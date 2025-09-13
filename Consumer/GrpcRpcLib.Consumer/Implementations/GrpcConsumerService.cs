using Grpc.Core;
using GrpcRpcLib.Consumer.Abstractions;
using GrpcRpcLib.Consumer.Dtos.Configurations;
using GrpcRpcLib.Shared.MessageTools.Abstraction;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using GrpcRpcLib.Consumer.Dtos.Attributes;
using GrpcRpcLib.Consumer.Protos;
using Microsoft.Extensions.Options;
using MessageEnvelope = GrpcRpcLib.Shared.Entities.Models.MessageEnvelope;
using LoggingOptions= SerilogLogger.Abstraction.Dtos.LoggingOptions;
using LogLevel = SerilogLogger.Abstraction.Enums.LogLevel;
using System.Runtime.CompilerServices;


namespace GrpcRpcLib.Consumer.Implementations;

public class GrpcConsumerService : GrpcReceiver.GrpcReceiverBase
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IMessageStore _store;
	private readonly GrpcRpcConsumerConfiguration _config;
	private readonly ConcurrentDictionary<string, (MethodInfo Method, Type ParameterType, object Instance)> _receivers = new();
	public CancellationToken _cancellationToken { get; private set; }

	public event Action<int>? OnReceived;

	public Action<(
			LogLevel logLevel,
			string messageTemplate,
			Dictionary<string, object?>? properties,
			Exception? exception,
			LoggingOptions? options,
			string methodName,
			string callerPath)>?
		LogAction;

	public Action<MessageEnvelope, (
		LogLevel logLevel,
		string messageTemplate,
		Dictionary<string, object?>? properties,
		Exception? exception,
		LoggingOptions? options,
		string methodName,
		string callerPath
		)>? OnError;


	public GrpcConsumerService(
		IServiceProvider serviceProvider, 
		IMessageStore store,
		IOptions<GrpcRpcConsumerConfiguration> config)
	{
		_serviceProvider = serviceProvider;
		_store = store;
		_config = config.Value;
	}

	public async Task<(bool success,string errorMessage)> Initialize(CancellationToken ct = default)
	{
		try
		{
			_cancellationToken = ct;

			RegisterReceivers();

			return (true,"");
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}

	public override async Task<ProcessResponse> Process(Protos.MessageEnvelope request, ServerCallContext context)
	{
		MessageEnvelope? envelope = null;

		try
		{
			envelope = MapToEnvelope(request);

			LogAction?.Invoke(
				(
					LogLevel.Information, 
					"Before processing",
					new Dictionary<string, object>()
					{
						{"methodName","Process"},
						{"className","GrpcConsumerService"}

					}, 
					null, 
					null,
					null,
					null)!);

			await _store.SaveAsync(envelope, context.CancellationToken);

			envelope.Status = "Received";

			await _store.UpdateStatusAsync(envelope.Id, "Received", context.CancellationToken);

			if (!_receivers.TryGetValue(envelope.Type, out var receiverInfo))
			{
				envelope.Status = "Failed";

				envelope.ErrorMessage = "No receiver found";

				await _store.UpdateStatusAsync(envelope.Id, "Failed", envelope.ErrorMessage, context.CancellationToken);

				OnError?.Invoke(envelope, (LogLevel.Error, "No receiver found", null, null, null, nameof(Process), nameof(GrpcConsumerService)));

				return new ProcessResponse { Success = false, ErrorMessage = "No receiver found" };
			}

			var entity = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(envelope.Payload), receiverInfo.ParameterType);

			await InvokeReceiver(receiverInfo, entity);

			envelope.Status = "Completed";

			await _store.UpdateStatusAsync(envelope.Id, "Completed", context.CancellationToken);

			OnReceived?.Invoke(1);

			return new ProcessResponse { Success = true, ErrorMessage = "" };
		}
		catch (Exception ex)
		{
			if (envelope != null)
			{
				envelope.Status = "Failed";
				envelope.ErrorMessage = ex.Message;
				await _store.UpdateStatusAsync(envelope.Id, "Failed", ex.Message, context.CancellationToken);
				OnError?.Invoke(envelope, (LogLevel.Error, ex.Message, null, ex, null, nameof(Process), nameof(GrpcConsumerService)));
			}
			return new ProcessResponse { Success = false, ErrorMessage = ex.Message };
		}
	}

	private void RegisterReceivers([CallerMemberName] string methodName = null!, [CallerFilePath] string callerPath = null!)
	{
		using var scope = _serviceProvider.CreateScope();

		var scopeProvider = scope.ServiceProvider;

		// گرفتن همه تایپ‌ها از اسمبلی جاری
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();

		var allTypes = assemblies.SelectMany(x =>
		{
			try
			{
				return x.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				return e.Types.Where(t => t != null);
			}
			catch (Exception e)
			{
				return [];
			}
		})
		.Where(t => !t.IsInterface && !t.IsAbstract && typeof(IGrpcProcessor).IsAssignableFrom(t))
		.ToList();

		foreach (var type in allTypes)
		{
			// گرفتن نمونه سرویس
			var receiver = GetServiceInstance(scopeProvider, type);

			if (receiver == null) continue;

			var methods = type.GetMethods(
						BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
							.Where(m => m.DeclaringType != typeof(object)))

					.Where(m => m.GetCustomAttributes<GrpcProcessorAttribute>() != null)
					.ToList();

			foreach (var method in methods)
			{
				var attribute = method.GetCustomAttribute<GrpcProcessorAttribute>();

				if (attribute == null) continue;

				var parameter = method.GetParameters().FirstOrDefault();
				if (parameter == null) continue;

				var parameterType = parameter.ParameterType;

				if (method.DeclaringType != type && method.DeclaringType!.IsGenericType)
				{
					var baseType = type.BaseType;

					if (baseType != null && baseType.IsGenericType && baseType.GetGenericTypeDefinition() ==
						method.DeclaringType.GetGenericTypeDefinition())
					{
						parameterType = baseType.GetGenericArguments()[0];
					}
				}
				if (type.IsGenericType && !type.IsGenericTypeDefinition)
				{
					var genericArgument = type.GetGenericArguments()[0];
				}
				_receivers[attribute.ProcessorType] = (method, parameterType, receiver);
			}
		}

		LogAction?.Invoke(
			(
				LogLevel.Debug,
				"Regestration of processors completed.",
				new Dictionary<string, object>()
				{ {"number of processors",_receivers.Count} },
				null,
				null,
				methodName,
				callerPath)!
			);
	}

	private IGrpcProcessor? GetServiceInstance(IServiceProvider provider, Type type)
	{
		// 1. مستقیم با خود کلاس از DI بخواه
		var instance = provider.GetService(type) as IGrpcProcessor;
		if (instance != null) return instance;

		// 2. با اینترفیس IGrpcProcessor بخواه
		instance = provider.GetService(typeof(IGrpcProcessor)) as IGrpcProcessor;
		if (instance != null) return instance;

		// 3. همه اینترفیس‌هایی که کلاس پیاده‌سازی کرده رو بررسی کن
		var interfaces = type.GetInterfaces();
		foreach (var iface in interfaces)
		{
			instance = provider.GetService(iface) as IGrpcProcessor;
			if (instance != null) return instance;
		}

		// 4. همه کلاس‌های پدر رو بررسی کن
		var baseType = type.BaseType;

		while (baseType != null && baseType != typeof(object))
		{
			instance = provider.GetService(baseType) as IGrpcProcessor;

			if (instance != null) return instance;

			baseType = baseType.BaseType;
		}

		// 5. کلاس‌های فرزند رو بررسی کن
		var derivedTypes = GetDerivedTypes(type);

		foreach (var derivedType in derivedTypes)
		{
			instance = provider.GetService(derivedType) as IGrpcProcessor;

			if (instance != null) return instance;
		}

		// 6. اگر هیچ‌کدوم نبود، null برگردون
		return null;
	}

	private IEnumerable<Type> GetDerivedTypes(Type baseType)
	{
		var assembly = Assembly.GetEntryAssembly();
		if (assembly == null) return Enumerable.Empty<Type>();

		return assembly.GetTypes()
			.Where(t => baseType.IsAssignableFrom(t) && t != baseType && !t.IsAbstract)
			.ToList();
	}

	private async Task InvokeReceiver((MethodInfo Method, Type ParameterType, object Instance) receiverInfo, object entity)
	{
		var task = receiverInfo.Method.Invoke(receiverInfo.Instance, new[] { entity }) as Task;
		if (task != null)
			await task;
	}

	private MessageEnvelope MapToEnvelope(Protos.MessageEnvelope proto)
	{
		return new MessageEnvelope
		{
			Id = Guid.Parse(proto.Id),
			Type = proto.Type,
			CorrelationId = proto.CorrelationId,
			Priority = proto.Priority,
			ReplyTo = proto.ReplyTo,
			Payload = proto.Payload.ToByteArray(),
			CreatedAt = DateTime.Parse(proto.CreatedAt),
			Status = proto.Status,
			RetryCount = proto.RetryCount,
			LastRetryAt = string.IsNullOrEmpty(proto.LastRetryAt) ? null : DateTime.Parse(proto.LastRetryAt),
			ErrorMessage = proto.ErrorMessage
		};
	}
}