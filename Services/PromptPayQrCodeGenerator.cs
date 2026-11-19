using System.Globalization;
using System.Text;
using QRCoder;

namespace Gamesgiftshop.Services;

public static class PromptPayQrCodeGenerator
{
    private const string PromptPayAid = "A000000677010111";

    public static string GenerateDataUrl(string staticPayload, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(staticPayload))
        {
            throw new ArgumentException("A static PromptPay QR payload is required.", nameof(staticPayload));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The payment amount must be positive.");
        }

        var amountText = amount.ToString("0.00", CultureInfo.InvariantCulture);
        if (amountText.Length > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The payment amount is too large for a PromptPay QR.");
        }

        var fields = ParseTlv(Encoding.UTF8.GetBytes(staticPayload));
        if (GetValue(fields, "00") != "01")
        {
            throw new InvalidOperationException("The configured QR payload has an unsupported format.");
        }

        var initiationIndex = FindUniqueField(fields, "01");
        if (GetValue(fields[initiationIndex]) != "11")
        {
            throw new InvalidOperationException("The configured QR payload must be a static PromptPay QR.");
        }

        fields[initiationIndex] = new TlvField("01", Encoding.ASCII.GetBytes("12"));

        var hasPromptPayAccount = fields
            .Where(field => IsMerchantAccountTag(field.Tag))
            .SelectMany(field => ParseTlv(field.Value))
            .Any(field => field.Tag == "00" && GetValue(field) == PromptPayAid);
        if (!hasPromptPayAccount)
        {
            throw new InvalidOperationException("The configured QR payload does not contain a PromptPay account.");
        }

        var currencyIndex = FindUniqueField(fields, "53");
        if (GetValue(fields[currencyIndex]) != "764")
        {
            throw new InvalidOperationException("The configured QR payload is not denominated in Thai baht.");
        }

        var amountIndices = fields
            .Select((field, index) => (field, index))
            .Where(item => item.field.Tag == "54")
            .Select(item => item.index)
            .ToList();
        if (amountIndices.Count > 1)
        {
            throw new InvalidOperationException("The configured QR payload contains multiple amount fields.");
        }

        var amountField = new TlvField("54", Encoding.ASCII.GetBytes(amountText));
        if (amountIndices.Count == 1)
        {
            fields[amountIndices[0]] = amountField;
        }
        else
        {
            fields.Insert(currencyIndex + 1, amountField);
        }

        var checksumIndex = FindUniqueField(fields, "63");
        if (checksumIndex != fields.Count - 1 || fields[checksumIndex].Value.Length != 4)
        {
            throw new InvalidOperationException("The configured QR payload has an invalid checksum field.");
        }
        fields.RemoveAt(checksumIndex);

        var checksumInput = SerializeTlv(fields)
            .Concat(Encoding.ASCII.GetBytes("6304"))
            .ToArray();
        var checksum = CalculateCrc16(checksumInput)
            .ToString("X4", CultureInfo.InvariantCulture);
        var payloadBytes = checksumInput
            .Concat(Encoding.ASCII.GetBytes(checksum))
            .ToArray();
        var payload = Encoding.UTF8.GetString(payloadBytes);

        using var qrData = new QRCodeGenerator().CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var image = new PngByteQRCode(qrData).GetGraphic(10);
        return $"data:image/png;base64,{Convert.ToBase64String(image)}";
    }

    private static bool IsMerchantAccountTag(string tag)
    {
        return int.TryParse(tag, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value is >= 26 and <= 51;
    }

    private static int FindUniqueField(IReadOnlyList<TlvField> fields, string tag)
    {
        var matches = fields
            .Select((field, index) => (field, index))
            .Where(item => item.field.Tag == tag)
            .Select(item => item.index)
            .ToList();
        if (matches.Count != 1)
        {
            throw new InvalidOperationException($"The configured QR payload must contain exactly one {tag} field.");
        }

        return matches[0];
    }

    private static string GetValue(IReadOnlyList<TlvField> fields, string tag)
    {
        return GetValue(fields[FindUniqueField(fields, tag)]);
    }

    private static string GetValue(TlvField field)
    {
        return Encoding.UTF8.GetString(field.Value);
    }

    private static List<TlvField> ParseTlv(ReadOnlySpan<byte> payload)
    {
        var fields = new List<TlvField>();
        var offset = 0;
        while (offset < payload.Length)
        {
            if (payload.Length - offset < 4)
            {
                throw new InvalidOperationException("The configured QR payload contains a truncated field.");
            }

            var tag = Encoding.ASCII.GetString(payload.Slice(offset, 2));
            var lengthText = Encoding.ASCII.GetString(payload.Slice(offset + 2, 2));
            if (!int.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
            {
                throw new InvalidOperationException("The configured QR payload contains an invalid field length.");
            }

            offset += 4;
            if (length > payload.Length - offset)
            {
                throw new InvalidOperationException("The configured QR payload contains a truncated field value.");
            }

            fields.Add(new TlvField(tag, payload.Slice(offset, length).ToArray()));
            offset += length;
        }

        return fields;
    }

    private static byte[] SerializeTlv(IEnumerable<TlvField> fields)
    {
        using var stream = new MemoryStream();
        foreach (var field in fields)
        {
            if (field.Tag.Length != 2 || field.Value.Length > 99)
            {
                throw new InvalidOperationException("The QR payload contains a field that is too large.");
            }

            var header = Encoding.ASCII.GetBytes(
                field.Tag + field.Value.Length.ToString("D2", CultureInfo.InvariantCulture));
            stream.Write(header);
            stream.Write(field.Value);
        }

        return stream.ToArray();
    }

    private static ushort CalculateCrc16(IEnumerable<byte> payload)
    {
        var crc = (ushort)0xFFFF;
        foreach (var value in payload)
        {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ 0x1021)
                    : (ushort)(crc << 1);
            }
        }

        return crc;
    }

    private sealed record TlvField(string Tag, byte[] Value);
}
