using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcRpcLib.Publisher.Configurations;
using GrpcRpcLib.Publisher.Protos;
using GrpcRpcLib.Shared.MessageTools.Abstraction;
using GrpcRpcLib.Shared.MessageTools.DataBase;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using SerilogLogger.Abstraction.LoggerInterface;
using MessageEnvelope = GrpcRpcLib.Shared.Entities.Models.MessageEnvelope;
using LoggingOptions =SerilogLogger.Abstraction.Dtos.LoggingOptions;
using LogLevel = SerilogLogger.Abstraction.Enums.LogLevel;


namespace GrpcRpcLib.Publisher.Services;

public class GrpcPublisher
{
	private readonly GrpcPublisherConfiguration _config;
	private readonly IMessageStore _store;
	private readonly ResiliencePipeline _retryPipeline;
	private readonly SemaphoreSlim _recoveryLock = new SemaphoreSlim(1, 1); // فقط برای تغییر mode و channel
	private readonly MessageDbContext _dbContext;
	private readonly AddressResolver _addressResolver;
	private volatile bool _isRecoveryMode = false; // volatile برای thread-safety
	private GrpcChannel? _channel;
	private string _lastTargetHostAddress = string.Empty;
	private string _lastReplyToAddress = string.Empty;
	private int _isWorkingTarget = 0; // volatile برای check

	public event Action<int>? OnPublished;

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

	public CancellationToken CancellationToken { get; private set; }

	public GrpcPublisher(
		IOptions<GrpcPublisherConfiguration> config,
		IMessageStore store,
		ILog logger,
		MessageDbContext dbContext,
		AddressResolver addressResolver)
	{
		_config = config.Value;
		_store = store;
		_dbContext = dbContext;
		_addressResolver = addressResolver;

		_retryPipeline = new ResiliencePipelineBuilder()
			.AddRetry(new RetryStrategyOptions
			{
				ShouldHandle = new PredicateBuilder()
					.Handle<RpcException>(ex => ex.StatusCode == StatusCode.Unavailable),
				DelayGenerator = args => ValueTask.FromResult<TimeSpan?>(TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber))),
				MaxRetryAttempts = int.MaxValue,
				OnRetry = args =>
				{
					LogAction?.Invoke(
						(LogLevel.Warning,
						$"Retry after {args.RetryDelay} (attempt {args.AttemptNumber})",
						null,
						args.Outcome.Exception,
						null,
						"RetryMethod",
						"GrpcPublisher"
						)); 
					return default;
				}
			})
			.Build();

	}

	public async Task<(bool success,string errorMessage)> Initialize(CancellationToken ct = default)
	{
		try
		{
			CancellationToken = ct;

			_lastTargetHostAddress = await _addressResolver.GetTargetHostAsync();

			_channel = GrpcChannel.ForAddress(_lastTargetHostAddress);

			_lastReplyToAddress = await _addressResolver.GetReplyToAsync();

			_ = Task.Run(() => RetryPendingAsync(ct), ct);

			return (true,"");
		}
		catch (Exception e)
		{
			return (success:false,
					errorMessage: e.ToString());
		}
		
	}

	public async Task<(bool success, string errorMessage)> SendAsync(
		MessageEnvelope envelope,
		[CallerMemberName] string methodName = null!, 
		[CallerFilePath] string callerPath = null!
		)
	{
		envelope.CorrelationId = envelope.Id.ToString();

		envelope.ReplyTo = _lastReplyToAddress;

		if (_isRecoveryMode)
		{
			envelope.Status = "Failed";

			await _store.SaveAsync(envelope, CancellationToken);

			OnError?.Invoke(envelope, (
				LogLevel.Error,
					"In recovery mode",
				null,
				null,
				null,
				methodName,
				callerPath
				));


			return (false, "Recovery mode: Message queued as Failed");
		}

		try
		{
			var client = new GrpcReceiver.GrpcReceiverClient(_channel!);

			var protoEnvelope = MapToProto(envelope);

			var responseProto = await client.ProcessAsync(protoEnvelope, deadline: DateTime.UtcNow.Add(TimeSpan.FromSeconds(_config.TimeoutDurationInSeconds)), cancellationToken: CancellationToken);

			if (responseProto.Success)
			{
				await _store.UpdateStatusAsync(envelope.Id, "Completed", CancellationToken);

				OnPublished?.Invoke(1);

				return (true, string.Empty);
			}
			else
			{
				await _store.UpdateStatusAsync(envelope.Id, "Failed", responseProto.ErrorMessage, CancellationToken);

				OnError?.Invoke(envelope, 
					(LogLevel.Error, responseProto.ErrorMessage, null, null, null, methodName, callerPath));

				//// اگر !success، وارد recovery mode
				//if (Interlocked.CompareExchange(ref _isWorkingTarget, 0, 1) == 1) // اگر قبلاً working بود
				//{
				//	await _recoveryLock.WaitAsync(CancellationToken);
				//	try
				//	{
				//		_isRecoveryMode = true;
				//		_ = Task.Run(() => RecoveryModeAsync(CancellationToken), CancellationToken);
				//	}
				//	finally
				//	{
				//		_recoveryLock.Release();
				//	}
				//}

				return (false, responseProto.ErrorMessage);
			}
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
		{
			envelope.Status = "Failed";
			envelope.ErrorMessage = ex.Message;
			await _store.SaveAsync(envelope, CancellationToken);
			OnError?.Invoke(envelope, (LogLevel.Error, "Server down", null, ex, null, methodName, callerPath));

			// وارد recovery mode
			if (Interlocked.CompareExchange(ref _isWorkingTarget, 0, 1) == 1)
			{
				await _recoveryLock.WaitAsync(CancellationToken);
				try
				{
					_addressResolver.InvalidateCache();
					var newAddress = await _addressResolver.GetTargetHostAsync(forceRefresh: true);
					if (newAddress != _lastTargetHostAddress)
					{
						_lastTargetHostAddress = newAddress;
						_channel?.Dispose();
						_channel = GrpcChannel.ForAddress(newAddress);
					}
					_isRecoveryMode = true;

					_ = Task.Run(() => RecoveryModeAsync(CancellationToken), CancellationToken);
				}
				finally
				{
					_recoveryLock.Release();
				}
			}

			return (false, "Recovery mode activated");
		}
	}

	private async Task RecoveryModeAsync(CancellationToken ct)
	{
		var context = ResilienceContextPool.Shared.Get(ct);
		try
		{
			await _retryPipeline.ExecuteAsync(async ctx =>
			{
				ctx.CancellationToken.ThrowIfCancellationRequested();

				if (await IsTargetHostWorkingAsync(ctx.CancellationToken))
				{
					Interlocked.Exchange(ref _isWorkingTarget, 1);
					_isRecoveryMode = false;
				}
				else
				{
					throw new RpcException(new Status(StatusCode.Unavailable, "Retry"));
				}
			}, context);
		}
		finally
		{
			ResilienceContextPool.Shared.Return(context);
		}
	}

	private async Task<bool> IsTargetHostWorkingAsync(CancellationToken ct = default)
	{
		if (_channel == null) return false;

		var client = new GrpcReceiver.GrpcReceiverClient(_channel);

		try
		{
			var testMessage = new TestMessageType() { };
			var jsonString = JsonConvert.SerializeObject(testMessage);
			var testEnvelope = new Protos.MessageEnvelope
			{
				Type = "test",
				Payload = Google.Protobuf.ByteString.CopyFromUtf8(jsonString),
				CorrelationId = Guid.NewGuid().ToString()
			};
			var response = await client.ProcessAsync(testEnvelope, deadline: DateTime.UtcNow.AddSeconds(5), cancellationToken: ct);
			return response.Success;
		}
		catch
		{
			Interlocked.Exchange(ref _isWorkingTarget, 0);
			return false;
		}
	}

	[Obsolete("Use AddressResolver.GetTargetHostAsync instead")]
	private async Task<string> GetAddressFromDbAsync()
	{
		return await _addressResolver.GetTargetHostAsync(forceRefresh: true);
	}

	private async Task RetryPendingAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				if (_isWorkingTarget == 1) // فقط اگر working
				{
					// دریافت pending با sort: Priority desc, then CreatedAt asc
					var pending = await _store.GetPendingAsync(ct); // فرض: implementation sort داره
					// اگر store sort نداره، اینجا sort کن
					var sortedPending = pending.OrderByDescending(m => m.Priority).ThenBy(m => m.CreatedAt).ToList();

					int count = 0;

					foreach (var msg in sortedPending)
					{
						var result = await ReSendAsync(msg);

						if (result.success)
						{
							count++;
						}
						else if (msg.RetryCount >= _config.MaxRetryCount)
						{
							await _store.UpdateStatusAsync(msg.Id, "Failed", result.errorMessage, ct);
						}
						else
						{
							await _store.IncrementRetryCountAsync(msg.Id, ct);
						}
					}
					OnPublished?.Invoke(count);
				}
				await Task.Delay(60000, ct);
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				LogAction?.Invoke((
					logLevel: LogLevel.Error,
					messageTemplate: "Retry loop failed",
					properties:null,
					exception:ex,
					loggingOptions:null,
					"RetryPendingAsync",
					"GrpcPublisher"
				));

				await Task.Delay(5000, ct);
			}
		}
	}

	private async Task<(bool success, string errorMessage)> ReSendAsync(MessageEnvelope envelope, [CallerMemberName] string methodName = null!, [CallerFilePath] string callerPath = null!)
	{
		envelope.CorrelationId = envelope.Id.ToString();

		envelope.ReplyTo = _lastReplyToAddress;


		if (_isRecoveryMode)
		{
			OnError?.Invoke(envelope, (LogLevel.Error, "In recovery mode", null, null, null, methodName, callerPath));

			return (false, "Recovery mode: Message queued as Failed");
		}

		try
		{
			var client = new GrpcReceiver.GrpcReceiverClient(_channel!);

			var protoEnvelope = MapToProto(envelope);

			var responseProto = await client.ProcessAsync(protoEnvelope, deadline: DateTime.UtcNow.Add(TimeSpan.FromSeconds(_config.TimeoutDurationInSeconds)), cancellationToken: CancellationToken);

			if (responseProto.Success)
			{
				await _store.UpdateStatusAsync(envelope.Id, "Completed", CancellationToken);

				OnPublished?.Invoke(1);

				return (true, string.Empty);
			}
			else
			{
				await _store.UpdateStatusAsync(envelope.Id, "Failed", responseProto.ErrorMessage, CancellationToken);

				OnError?.Invoke(envelope, (LogLevel.Error, responseProto.ErrorMessage, null, null, null, methodName, callerPath));

				// اگر !success، وارد recovery mode
				if (Interlocked.CompareExchange(ref _isWorkingTarget, 0, 1) == 1) // اگر قبلاً working بود
				{
					await _recoveryLock.WaitAsync(CancellationToken);
					try
					{
						_isRecoveryMode = true;
						_ = Task.Run(() => RecoveryModeAsync(CancellationToken), CancellationToken);
					}
					finally
					{
						_recoveryLock.Release();
					}
				}

				return (false, responseProto.ErrorMessage);
			}
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
		{
			envelope.Status = "Failed";
			envelope.ErrorMessage = ex.Message;
			await _store.SaveAsync(envelope, CancellationToken);
			OnError?.Invoke(envelope, (LogLevel.Error, "Server down", null, ex, null, methodName, callerPath));

			// وارد recovery mode
			if (Interlocked.CompareExchange(ref _isWorkingTarget, 0, 1) == 1)
			{
				await _recoveryLock.WaitAsync(CancellationToken);
				try
				{
					_addressResolver.InvalidateCache();
					var newAddress = await _addressResolver.GetTargetHostAsync(forceRefresh: true);
					if (newAddress != _lastTargetHostAddress)
					{
						_lastTargetHostAddress = newAddress;
						_channel?.Dispose();
						_channel = GrpcChannel.ForAddress(newAddress);
					}
					_isRecoveryMode = true;

					_ = Task.Run(() => RecoveryModeAsync(CancellationToken), CancellationToken);
				}
				finally
				{
					_recoveryLock.Release();
				}
			}

			return (false, "Recovery mode activated");
		}
	}

	private GrpcRpcLib.Publisher.Protos.MessageEnvelope MapToProto(MessageEnvelope envelope)
	{
		return new Protos.MessageEnvelope
		{
			Id = envelope.Id.ToString(),
			Type = envelope.Type,
			CorrelationId = envelope.CorrelationId,
			Priority = envelope.Priority,
			ReplyTo = envelope.ReplyTo,
			Payload = Google.Protobuf.ByteString.CopyFrom(envelope.Payload),
			CreatedAt = envelope.CreatedAt.ToString("o"),
			Status = envelope.Status,
			RetryCount = envelope.RetryCount,
			LastRetryAt = envelope.LastRetryAt?.ToString("o") ?? "",
			ErrorMessage = envelope.ErrorMessage ?? ""
		};
	}

	public class GrpcRetryHostedService : BackgroundService
	{
		private readonly GrpcPublisher _publisher;
		private readonly ILogger<GrpcRetryHostedService> _logger;

		public GrpcRetryHostedService(GrpcPublisher publisher, ILogger<GrpcRetryHostedService> logger)
		{
			_publisher = publisher;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				await _publisher.RetryPendingAsync(stoppingToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Background retry service failed");
			}
		}
	}
}