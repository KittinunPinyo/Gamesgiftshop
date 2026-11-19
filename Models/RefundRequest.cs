namespace Gamesgiftshop.Models;

public class RefundRequest
{
    public const string PendingStatus = "รอตรวจสอบ";
    public const string AwaitingReturnStatus = "รอส่งคืนสินค้า";
    public const string ReturnShippedStatus = "รอตรวจรับสินค้า";
    public const string ReturnReceivedStatus = "ตรวจรับสินค้าแล้ว";
    public const string RefundedStatus = "คืนเงินแล้ว";
    public const string RejectedStatus = "ปฏิเสธ";

    public int RefundRequestId { get; set; }

    public int OrderId { get; set; }

    public int UserId { get; set; }

    public string Reason { get; set; } = "";

    public string Status { get; set; } = PendingStatus;

    public string? Feedback { get; set; }

    public string? ReturnAddress { get; set; }

    public string? ReturnTrackingNumber { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedBy { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual RefundEvidence? Evidence { get; set; }
}

public class RefundEvidence
{
    public int RefundRequestId { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "";
    public RefundRequest Request { get; set; } = null!;
}
