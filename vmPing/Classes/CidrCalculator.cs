using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text.RegularExpressions;

namespace vmPing.Classes
{
    public static class CidrCalculator
    {
        // ========== IPv4 CIDR Calculation ==========
        public static CidrResult CalculateIpv4Cidr(string ipText, int prefix)
        {
            var octets = ParseIpv4Address(ipText);
            if (octets == null) return null;

            uint ip = IpToUint(octets);
            int hostBits = 32 - prefix;
            uint blockSize = (uint)(1L << hostBits);
            uint mask = prefix == 0 ? 0 : (0xffffffffu << hostBits);
            uint network = ip & mask;
            uint broadcast = network + blockSize - 1;

            string firstUsable, lastUsable;
            int usableHosts;
            string broadcastText;
            string note;

            if (prefix < 31 && blockSize > 2)
            {
                firstUsable = UintToIp(network + 1);
                lastUsable = UintToIp(broadcast - 1);
                usableHosts = (int)blockSize - 2;
                broadcastText = UintToIp(broadcast);
                note = "常规子网";
            }
            else if (prefix == 31)
            {
                firstUsable = UintToIp(network);
                lastUsable = UintToIp(broadcast);
                usableHosts = 2;
                broadcastText = "无（/31 点对点链路）";
                note = "/31 点对点链路，无广播";
            }
            else // /32
            {
                firstUsable = UintToIp(network);
                lastUsable = UintToIp(network);
                usableHosts = 1;
                broadcastText = "无（/32 单主机）";
                note = "/32 单主机";
            }

            return new CidrResult
            {
                Version = "IPv4",
                Cidr = $"{UintToIp(network)}/{prefix}",
                Mask = PrefixToMask(prefix),
                Wildcard = PrefixToWildcard(prefix),
                Details = new List<CidrDetail>
                {
                    new CidrDetail { Label = "子网掩码", Value = PrefixToMask(prefix) },
                    new CidrDetail { Label = "网络地址", Value = UintToIp(network) },
                    new CidrDetail { Label = "广播地址", Value = broadcastText },
                    new CidrDetail { Label = "网段块大小", Value = $"{blockSize:N0} 个地址" },
                    new CidrDetail { Label = "首个可用 IP", Value = firstUsable },
                    new CidrDetail { Label = "最后可用 IP", Value = lastUsable },
                    new CidrDetail { Label = "总地址数", Value = $"{blockSize:N0}" },
                    new CidrDetail { Label = "可用主机数", Value = $"{usableHosts:N0}" },
                    new CidrDetail { Label = "反掩码", Value = PrefixToWildcard(prefix) },
                    new CidrDetail { Label = "IP 地址类型", Value = GetIpv4AddressType(octets) },
                    new CidrDetail { Label = "备注", Value = note }
                }
            };
        }

        // ========== IPv6 CIDR Calculation ==========
        public static CidrResult CalculateIpv6Cidr(ushort[] groups, int prefix)
        {
            var ip = Ipv6GroupsToBigInt(groups);
            int hostBits = 128 - prefix;
            var addressCount = BigInteger.One << hostBits;
            var mask = prefix == 0 ? BigInteger.Zero : ((BigInteger.One << prefix) - 1) << hostBits;
            var network = ip & mask;
            var last = network + addressCount - 1;

            var networkGroups = BigIntToIpv6Groups(network);
            var lastGroups = BigIntToIpv6Groups(last);

            return new CidrResult
            {
                Version = "IPv6",
                Cidr = $"{FormatIpv6Compressed(groups)}/{prefix}",
                Details = new List<CidrDetail>
                {
                    new CidrDetail { Label = "网络前缀", Value = $"{FormatIpv6Compressed(networkGroups)}/{prefix}" },
                    new CidrDetail { Label = "前缀长度", Value = $"/{prefix}" },
                    new CidrDetail { Label = "网络地址", Value = FormatIpv6Compressed(networkGroups) },
                    new CidrDetail { Label = "范围结束地址", Value = FormatIpv6Compressed(lastGroups) },
                    new CidrDetail { Label = "地址数量", Value = $"{addressCount:N0}" },
                    new CidrDetail { Label = "前缀掩码", Value = PrefixToIpv6Mask(prefix) },
                    new CidrDetail { Label = "IPv6 地址类型", Value = GetIpv6AddressType(groups) },
                    new CidrDetail { Label = "完整展开", Value = FormatIpv6Full(groups) }
                }
            };
        }

        // ========== VLSM Planning ==========
        public static VlsmResult PlanVlsm(string baseCidrText, List<VlsmRequirement> requirements)
        {
            var parsed = ParseIpv4CidrString(baseCidrText);
            if (!parsed.Ok) return new VlsmResult { Ok = false, Message = parsed.Message };

            if (requirements == null || requirements.Count == 0)
                return new VlsmResult { Ok = false, Message = "请至少添加一个子网需求。" };

            var items = new List<VlsmItem>();
            for (int i = 0; i < requirements.Count; i++)
            {
                var req = requirements[i];
                if (req.Hosts < 1)
                    return new VlsmResult { Ok = false, Message = $"第 {i + 1} 个子网的主机数需为不小于 1 的整数。" };

                int minBlock = req.Hosts + 2;
                int hostBits = 0;
                while ((1 << hostBits) < minBlock) hostBits++;
                if (hostBits > 32 - parsed.Cidr)
                    return new VlsmResult { Ok = false, Message = $"第 {i + 1} 个子网（需要 {req.Hosts} 台主机）超出主网段容量。" };

                items.Add(new VlsmItem
                {
                    OriginalIndex = i,
                    Name = string.IsNullOrEmpty(req.Name) ? $"子网 {i + 1}" : req.Name,
                    Hosts = req.Hosts,
                    HostBits = hostBits,
                    BlockSize = 1 << hostBits,
                    Prefix = 32 - hostBits
                });
            }

            items.Sort((a, b) => b.BlockSize.CompareTo(a.BlockSize));

            uint baseStart = IpToUint(parsed.Octets) & (parsed.Cidr == 0 ? 0u : (0xffffffffu << (32 - parsed.Cidr)));
            uint baseEnd = baseStart + (uint)(1L << (32 - parsed.Cidr)) - 1;

            uint cursor = baseStart;
            var allocations = new List<VlsmAllocation>();

            foreach (var item in items)
            {
                uint aligned = Align(cursor, (uint)item.BlockSize);
                if (aligned + item.BlockSize - 1 > baseEnd)
                    return new VlsmResult { Ok = false, Message = $"主网段容量不足以容纳所有子网，已分配 {allocations.Count} 个，\"{item.Name}\" 无法分配。" };

                uint network = aligned;
                uint broadcast = network + (uint)item.BlockSize - 1;
                int usable = item.HostBits >= 2 ? item.BlockSize - 2 : (item.HostBits == 1 ? 2 : 1);

                allocations.Add(new VlsmAllocation
                {
                    Name = item.Name,
                    RequestedHosts = item.Hosts,
                    Cidr = $"{UintToIp(network)}/{item.Prefix}",
                    Network = UintToIp(network),
                    Broadcast = item.HostBits >= 2 ? UintToIp(broadcast) : "—",
                    Mask = PrefixToMask(item.Prefix),
                    Prefix = item.Prefix,
                    BlockSize = item.BlockSize,
                    UsableHosts = usable,
                    FirstHost = item.HostBits >= 2 ? UintToIp(network + 1) : UintToIp(network),
                    LastHost = item.HostBits >= 2 ? UintToIp(broadcast - 1) : UintToIp(broadcast)
                });

                cursor = broadcast + 1;
            }

            return new VlsmResult
            {
                Ok = true,
                Base = $"{UintToIp(baseStart)}/{parsed.Cidr}",
                BaseStart = UintToIp(baseStart),
                BaseEnd = UintToIp(baseEnd),
                Allocations = allocations.OrderBy(a => a.Cidr).ToList()
            };
        }

        // ========== Subnet Enumeration ==========
        public static SubnetEnumResult EnumerateSubnets(string baseCidrText, int targetPrefix)
        {
            var parsed = ParseIpv4CidrString(baseCidrText);
            if (!parsed.Ok) return new SubnetEnumResult { Ok = false, Message = parsed.Message };

            if (targetPrefix < parsed.Cidr)
                return new SubnetEnumResult { Ok = false, Message = "目标前缀必须大于或等于大网段前缀。" };

            int totalCount = 1 << (targetPrefix - parsed.Cidr);
            if (totalCount > 2048)
                return new SubnetEnumResult { Ok = false, Message = $"本次会生成 {totalCount:N0} 个子网，超过页面展示上限 2048 个。" };

            uint baseIp = IpToUint(parsed.Octets);
            uint baseMask = parsed.Cidr == 0 ? 0u : (0xffffffffu << (32 - parsed.Cidr));
            uint baseStart = baseIp & baseMask;
            uint baseSize = (uint)(1L << (32 - parsed.Cidr));
            uint baseEnd = baseStart + baseSize - 1;
            uint subnetSize = (uint)(1L << (32 - targetPrefix));

            var subnets = new List<SubnetInfo>();
            for (int i = 0; i < totalCount; i++)
            {
                uint start = baseStart + (uint)i * subnetSize;
                uint end = start + subnetSize - 1;
                int usableHosts = targetPrefix < 31 ? (int)subnetSize - 2 : (targetPrefix == 31 ? 2 : 1);
                subnets.Add(new SubnetInfo
                {
                    Index = i + 1,
                    Cidr = $"{UintToIp(start)}/{targetPrefix}",
                    Network = UintToIp(start),
                    Broadcast = targetPrefix <= 30 ? UintToIp(end) : "—",
                    Mask = PrefixToMask(targetPrefix),
                    Size = subnetSize,
                    UsableHosts = usableHosts
                });
            }

            return new SubnetEnumResult
            {
                Ok = true,
                Base = $"{UintToIp(baseStart)}/{parsed.Cidr}",
                TargetPrefix = targetPrefix,
                TotalCount = totalCount,
                Subnets = subnets
            };
        }

        // ========== IP Conversion ==========
        public static IpConvertResult ConvertIpValue(string input)
        {
            input = input?.Trim() ?? "";
            if (string.IsNullOrEmpty(input))
                return new IpConvertResult { Ok = false, Message = "请输入 IP、十进制整数或十六进制。" };

            uint value;
            string sourceType;

            if (input.Contains('.'))
            {
                var octets = ParseIpv4Address(input);
                if (octets == null) return new IpConvertResult { Ok = false, Message = "IPv4 地址格式不正确。" };
                value = IpToUint(octets);
                sourceType = "IPv4 地址";
            }
            else if (Regex.IsMatch(input, "^0x[0-9a-fA-F]{1,8}$"))
            {
                value = Convert.ToUInt32(input.Substring(2), 16);
                sourceType = "十六进制";
            }
            else if (Regex.IsMatch(input, "^[0-9a-fA-F]{8}$") && Regex.IsMatch(input, "[a-fA-F]"))
            {
                value = Convert.ToUInt32(input, 16);
                sourceType = "十六进制";
            }
            else if (Regex.IsMatch(input, "^[01]{32}$"))
            {
                value = Convert.ToUInt32(input, 2);
                sourceType = "二进制";
            }
            else if (Regex.IsMatch(input, "^\\d+$"))
            {
                ulong dec = ulong.Parse(input);
                if (dec > 0xffffffff)
                    return new IpConvertResult { Ok = false, Message = "十进制整数需在 0 到 4294967295 之间。" };
                value = (uint)dec;
                sourceType = "十进制整数";
            }
            else
            {
                return new IpConvertResult { Ok = false, Message = "格式不支持。可输入 10.88.135.144、176719760、0x0A588790。" };
            }

            string ip = UintToIp(value);
            string hex = value.ToString("X8");
            string binary = Convert.ToString(value, 2).PadLeft(32, '0');

            return new IpConvertResult
            {
                Ok = true,
                SourceType = sourceType,
                Ip = ip,
                Decimal = value.ToString(),
                Hex = $"0x{hex}",
                Binary = Regex.Replace(binary, ".{8}", "$0 ").Trim()
            };
        }

        // ========== Helper Methods ==========
        private static uint IpToUint(byte[] octets)
        {
            return ((uint)octets[0] << 24) | ((uint)octets[1] << 16) | ((uint)octets[2] << 8) | octets[3];
        }

        private static string UintToIp(uint value)
        {
            return $"{(value >> 24) & 255}.{(value >> 16) & 255}.{(value >> 8) & 255}.{value & 255}";
        }

        private static byte[] ParseIpv4Address(string text)
        {
            var parts = text?.Trim().Split('.');
            if (parts?.Length != 4) return null;

            var octets = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (!byte.TryParse(parts[i], out octets[i])) return null;
            }
            return octets;
        }

        private static (bool Ok, byte[] Octets, int Cidr, string Message) ParseIpv4CidrString(string text)
        {
            text = text?.Trim() ?? "";
            var match = Regex.Match(text, @"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})/(\d{1,2})$");
            if (!match.Success)
                return (false, null, 0, "请输入形如 192.168.1.0/24 的 CIDR。");

            var octets = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (!byte.TryParse(match.Groups[i + 1].Value, out octets[i]))
                    return (false, null, 0, "IP 每段需为 0-255。");
            }

            if (!int.TryParse(match.Groups[5].Value, out int cidr) || cidr < 0 || cidr > 32)
                return (false, null, 0, "CIDR 需为 0-32。");

            return (true, octets, cidr, null);
        }

        private static string PrefixToMask(int prefix)
        {
            if (prefix == 0) return "0.0.0.0";
            return UintToIp((0xffffffffu << (32 - prefix)));
        }

        private static string PrefixToWildcard(int prefix)
        {
            uint mask = prefix == 0 ? 0 : (0xffffffffu << (32 - prefix));
            return UintToIp(~mask);
        }

        private static uint Align(uint value, uint size)
        {
            uint remainder = value % size;
            return remainder == 0 ? value : value + (size - remainder);
        }

        private static string GetIpv4AddressType(byte[] octets)
        {
            int first = octets[0];
            string className;
            if (first >= 1 && first <= 127) className = "A 类";
            else if (first >= 128 && first <= 191) className = "B 类";
            else if (first >= 192 && first <= 223) className = "C 类";
            else if (first >= 224 && first <= 239) className = "D 类";
            else className = "E 类";

            string ip = string.Join(".", octets);
            if (ip.StartsWith("10.") || ip.StartsWith("192.168.") ||
                (first == 172 && octets[1] >= 16 && octets[1] <= 31))
                return className + " 私网";
            if (first == 127) return className + " 环回";
            if (first == 169 && octets[1] == 254) return className + " 链路本地";
            if (first >= 224) return className + " 组播/保留";
            return className + " 公网";
        }

        // IPv6 helpers
        private static BigInteger Ipv6GroupsToBigInt(ushort[] groups)
        {
            BigInteger value = 0;
            for (int i = 0; i < groups.Length; i++)
                value = (value << 16) + groups[i];
            return value;
        }

        private static ushort[] BigIntToIpv6Groups(BigInteger value)
        {
            var groups = new ushort[8];
            for (int i = 7; i >= 0; i--)
                groups[7 - i] = (ushort)((value >> (i * 16)) & 0xffff);
            return groups;
        }

        private static string FormatIpv6Full(ushort[] groups)
        {
            return string.Join(":", groups.Select(g => g.ToString("x4")));
        }

        private static string FormatIpv6Compressed(ushort[] groups)
        {
            string full = string.Join(":", groups.Select(g => g.ToString("x")));
            // Simple compression: find longest run of :0:0:... and replace with ::
            // This is a simplified version
            return full;
        }

        private static string PrefixToIpv6Mask(int prefix)
        {
            if (prefix == 0) return "::";
            var mask = ((BigInteger.One << prefix) - 1) << (128 - prefix);
            return FormatIpv6Full(BigIntToIpv6Groups(mask));
        }

        private static string GetIpv6AddressType(ushort[] groups)
        {
            var value = Ipv6GroupsToBigInt(groups);
            if (value == 0) return "未指定地址 ::/128";
            if (value == 1) return "环回地址 ::1/128";
            if ((groups[0] & 0xffc0) == 0xfe80) return "链路本地地址 fe80::/10";
            if ((groups[0] & 0xfe00) == 0xfc00) return "ULA 私网 fc00::/7";
            if ((groups[0] & 0xff00) == 0xff00) return "多播地址 ff00::/8";
            if ((groups[0] & 0xe000) == 0x2000) return "全球单播地址 2000::/3";
            return "IPv6 特殊/保留地址";
        }
    }

    // Result classes
    public class CidrResult
    {
        public string Version { get; set; }
        public string Cidr { get; set; }
        public string Mask { get; set; }
        public string Wildcard { get; set; }
        public List<CidrDetail> Details { get; set; } = new List<CidrDetail>();
    }

    public class CidrDetail
    {
        public string Label { get; set; }
        public string Value { get; set; }
    }

    public class VlsmRequirement
    {
        public string Name { get; set; }
        public int Hosts { get; set; }
    }

    public class VlsmResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public string Base { get; set; }
        public string BaseStart { get; set; }
        public string BaseEnd { get; set; }
        public List<VlsmAllocation> Allocations { get; set; } = new List<VlsmAllocation>();
    }

    public class VlsmAllocation
    {
        public string Name { get; set; }
        public int RequestedHosts { get; set; }
        public string Cidr { get; set; }
        public string Network { get; set; }
        public string Broadcast { get; set; }
        public string Mask { get; set; }
        public int Prefix { get; set; }
        public int BlockSize { get; set; }
        public int UsableHosts { get; set; }
        public string FirstHost { get; set; }
        public string LastHost { get; set; }
    }

    public class SubnetEnumResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public string Base { get; set; }
        public int TargetPrefix { get; set; }
        public int TotalCount { get; set; }
        public List<SubnetInfo> Subnets { get; set; } = new List<SubnetInfo>();
    }

    public class SubnetInfo
    {
        public int Index { get; set; }
        public string Cidr { get; set; }
        public string Network { get; set; }
        public string Broadcast { get; set; }
        public string Mask { get; set; }
        public uint Size { get; set; }
        public int UsableHosts { get; set; }
    }

    public class IpConvertResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public string SourceType { get; set; }
        public string Ip { get; set; }
        public string Decimal { get; set; }
        public string Hex { get; set; }
        public string Binary { get; set; }
    }

    internal class VlsmItem
    {
        public int OriginalIndex { get; set; }
        public string Name { get; set; }
        public int Hosts { get; set; }
        public int HostBits { get; set; }
        public int BlockSize { get; set; }
        public int Prefix { get; set; }
    }
}
