namespace Tally.Domain.Entities;

// The uploaded picture itself. Kept in SQL so it survives restarts (cloud disks are temporary).
public class ReceiptImage : Entity
{
    public required string Key { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Bytes { get; set; }
}