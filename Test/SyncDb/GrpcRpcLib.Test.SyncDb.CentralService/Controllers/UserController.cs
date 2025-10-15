using System.Text;
using GrpcRpcLib.Publisher.Services;
using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Test.SyncDb.Shared.Abstractions;
using GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;
using GrpcRpcLib.Test.SyncDb.Shared.Dtos;
using GrpcRpcLib.Test.SyncDb.Shared.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace GrpcRpcLib.Test.SyncDb.CentralService.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class UserController(CentralDbContext db, GrpcPublisher publisher,ISequenceProvider sequenceProvider)
		: ControllerBase
	{
	
		[HttpGet] public async Task<IEnumerable<User>> Get() => await db.Users.OrderBy(x => x.Id).ToListAsync();

		[HttpPost]
		public async Task<IActionResult> Create(CreateUserDto dto)
		{
			try
			{
				if (await db.Users.AnyAsync(x => x.Id == dto.Id)) return Conflict("Id exists");

				var u = new User { Id = dto.Id, Username = dto.Username, FullName = dto.FullName };

				var addUserEventDto = new AddUserEventDto(u.Id, u.Username, u.FullName);

				var ev = new Event
				{
					EventId = Guid.NewGuid(),
					AggregateType = "User",
					AggregateId = u.Id,
					EventType = "AddUser",
					Payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(addUserEventDto)), // evDto همان AddUserEventDto که ساخته‌ای
					CreatedAt = DateTime.UtcNow
				};

				var trns = await db.Database.BeginTransactionAsync();

				var lastSeq = await sequenceProvider.GetNextSequenceAsync(db, ev.AggregateType, ev.AggregateId);

				db.Users.Add(u);

				db.Events.Add(ev);

				await db.SaveChangesAsync();

				return Ok($"User Add Successfully,UserId:{u.Id}");

				
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError);
			}
			// چون Id دستی است، همان را ذخیره می‌کنیم
			

			//// publish event به Consumer
			

			//var envelope = new MessageEnvelope
			//{
			//	Type = "AddUser",
			//	Payload =Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(ev)),
			//	CreatedAt = DateTime.UtcNow,
			//	CorrelationId = Guid.NewGuid().ToString(),
			//	Priority = 10
			//};

			//var (ok, err) = await publisher.SendAsync(envelope);

			
		}
	}
}
