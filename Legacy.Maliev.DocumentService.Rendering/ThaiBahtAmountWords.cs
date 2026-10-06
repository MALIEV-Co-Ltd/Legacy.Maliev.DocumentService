using System.Globalization;
using System.Text;

namespace Legacy.Maliev.DocumentService.Rendering;

/// <summary>Formats the displayed receipt amount as Thai baht and satang words.</summary>
public static class ThaiBahtAmountWords
{
    private static readonly string[] Digits = ["", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า"];
    private static readonly string[] Places = ["", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน"];

    /// <summary>
    /// Uses the same invariant N2 rounding as receipt numeric totals. Rounded zero is
    /// explicit; negative amounts retain their sign, and all decimal magnitudes are supported.
    /// </summary>
    /// <param name="amount">The receipt's actual amount paid.</param>
    /// <returns>Thai words for the displayed amount, without enclosing parentheses.</returns>
    public static string Format(decimal amount)
    {
        var displayed = amount.ToString("N2", CultureInfo.InvariantCulture).Replace(",", string.Empty, StringComparison.Ordinal);
        var negative = displayed.StartsWith('-');
        if (negative)
        {
            displayed = displayed[1..];
        }

        var parts = displayed.Split('.');
        var baht = ReadInteger(parts[0]);
        var satang = ReadGroup(parts[1].TrimStart('0'));
        if (baht.Length == 0 && satang.Length == 0)
        {
            return "ศูนย์บาทถ้วน";
        }

        return (negative ? "ลบ" : string.Empty)
            + (baht.Length == 0 ? string.Empty : baht + "บาท")
            + (satang.Length == 0 ? "ถ้วน" : satang + "สตางค์");
    }

    private static string ReadInteger(string digits) => digits.Length > 6
        ? ReadInteger(digits[..^6]) + "ล้าน" + ReadGroup(digits[^6..])
        : ReadGroup(digits);

    private static string ReadGroup(string digits)
    {
        var words = new StringBuilder();
        for (var index = 0; index < digits.Length; index++)
        {
            var digit = digits[index] - '0';
            if (digit == 0)
            {
                continue;
            }

            var place = digits.Length - index - 1;
            if (place == 0 && digit == 1 && digits.Length > 1)
            {
                words.Append("เอ็ด");
            }
            else if (place == 1 && digit == 2)
            {
                words.Append("ยี่");
            }
            else if (place != 1 || digit != 1)
            {
                words.Append(Digits[digit]);
            }

            words.Append(Places[place]);
        }

        return words.ToString();
    }
}
