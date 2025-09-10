using GrpcRpcLib.Consumer.Abstractions;
using GrpcRpcLib.Consumer.Dtos.Attributes;
using GrpcRpcLib.Shared.Entities.Models;

namespace GrpcRpcLib.Consumer.Implementations;

public class TestTypeProcessor:IGrpcProcessor
{
	[GrpcProcessor("test")]
	public void TestProcessor(TestMessageType test)
	{
		test.MessageContent += "1";
	}
}


public class TestMessageType
{
	public string MessageContent { get; set; } = string.Empty;
}