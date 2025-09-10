namespace GrpcRpcLib.Consumer.Dtos.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class GrpcProcessorAttribute(string processorType) : Attribute
{
	public string ProcessorType { get; } = processorType;
}