using GrpcRpcLib.Consumer.Abstractions;
using GrpcRpcLib.Consumer.Dtos.Attributes;
using GrpcRpcLib.Shared.Entities.Models;

namespace GrpcRpcLib.Consumer.Implementations;





public class TestTypeProcessor:IGrpcProcessor
//{
//	[GrpcProcessor("test")]
//	public void TestProcessor(Dictionary<string, string> test)
//	{

//		//var key = 


//		//var query = """
//		//            identity ooff;
//		//            INSERT INTO dbo.[....] ()
//		//			VALUE ();
//		//            identi on;
//		//            """;


//		//dapper.Executeq.....





//		test.me += "1";
//	}
{
	[GrpcProcessor("test")]
	public void TestProcessor(TestMessageType test)
	{

		//var key = 


		//var query = """
		//            identity ooff;
		//            INSERT INTO dbo.[....] ()
		//			VALUE ();
		//            identi on;
		//            """;


		//dapper.Executeq.....





		test.MessageContent += " 1";
	}
}


public class TestMessageType
{
	public string MessageContent { get; set; } = string.Empty;
}