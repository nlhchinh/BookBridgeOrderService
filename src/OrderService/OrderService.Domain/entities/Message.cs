using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderService.Domain.Entities
{
	[Table("Messages")]
	public class Message
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long Id { get; set; }

		[Required]
		[MaxLength(100)]
		public string EventType { get; set; } = string.Empty; // Event name: BookstorePublishEvent

		[MaxLength(100)]
		public string? ServiceName { get; set; }


		[Required]
		public string Payload { get; set; } = string.Empty;  // Json reponse

		[Required]
		[MaxLength(50)]
		public MessageStatus MessageStatus { get; set; } = MessageStatus.Pending;


		// Trace flow
		[MaxLength(255)]
		public string? TraceId { get; set; }


		[Required]
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public DateTime? PublishedAt { get; set; }

		[Required]
		public int RetryCount { get; set; } = 0;
		
		[Column(TypeName = "text")]
		public string? LastError { get; set; }

	}

	public enum MessageStatus
	{
		Pending,
		Published,
		Failed,
		Processing
	}
}
