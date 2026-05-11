using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HPPrinterScanner.Models;

namespace HPPrinterScanner.Services
{
    public class NetworkScannerService
    {
        // First 8 chars (XX:XX:XX) of MAC addresses registered to HP Inc / Hewlett-Packard.
        // Source: IEEE OUI registry filtered to HP entries.
        private static readonly HashSet<string> HpOuis = new(StringComparer.OrdinalIgnoreCase)
        {
            "00:00:92", "00:01:E6", "00:01:E7", "00:04:EA", "00:08:02",
            "00:0F:20", "00:10:83", "00:10:E3", "00:11:0A", "00:11:85",
            "00:12:79", "00:13:21", "00:14:38", "00:14:C2", "00:15:60",
            "00:16:35", "00:17:08", "00:17:A4", "00:18:71", "00:19:BB",
            "00:1A:4B", "00:1B:78", "00:1C:C4", "00:1D:09", "00:1E:0B",
            "00:1F:29", "00:21:5A", "00:22:64", "00:23:7D", "00:24:81",
            "00:25:B3", "00:26:55", "00:27:13", "00:30:C1", "00:50:8B",
            "00:60:B0", "00:80:5F", "08:00:09", "1C:C1:DE", "28:92:4A",
            "28:D2:44", "2C:27:D7", "2C:44:FD", "30:8D:99", "34:DA:B7",
            "38:63:BB", "3C:D9:2B", "40:A8:F0", "40:B0:34", "48:0F:CF",
            "4C:39:09", "50:65:F3", "54:E1:AD", "58:20:B1", "60:EB:69",
            "64:51:06", "68:B5:99", "6C:3B:6B", "70:5A:0F", "74:46:A0",
            "78:E3:B5", "7C:57:3C", "80:C1:6E", "84:34:97", "88:51:FB",
            "90:1B:0E", "94:57:A5", "98:4B:E1", "98:E7:F4", "9C:8E:99",
            "9C:B6:54", "A0:1D:48", "A0:48:1C", "A0:D3:C1", "A4:5D:36",
            "A8:97:DC", "AC:CF:85", "B0:5A:DA", "B4:99:BA", "B8:AF:67",
            "BC:EA:FA", "D4:85:64", "D8:9D:67", "E8:39:35", "F4:CE:46",
            "FC:15:B4", "10:60:4B", "14:58:D0", "18:A9:05", "E0:70:EA",
        };

        private static readonly HttpClient Http = new(
            new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                AllowAutoRedirect      = true,
            })
        {
            Timeout = TimeSpan.FromSeconds(12),
            DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" } },
        };

        public static string DetectLocalSubnet()
        {
            foreach (bool requireGateway in new[] { true, false })
            {
                foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (iface.OperationalStatus != OperationalStatus.Up ||
                        iface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        iface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        continue;

                    var props = iface.GetIPProperties();

                    if (requireGateway)
                    {
                        bool hasGateway = props.GatewayAddresses
                            .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                                   && !IPAddress.Any.Equals(g.Address));
                        if (!hasGateway) continue;
                    }

                    foreach (var addr in props.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                        byte[] ip   = addr.Address.GetAddressBytes();
                        byte[] mask = addr.IPv4Mask?.GetAddressBytes()
                                      ?? new byte[] { 255, 255, 255, 0 };

                        var parts = new List<string>();
                        for (int i = 0; i < 4; i++)
                        {
                            if (mask[i] == 0xFF) parts.Add(ip[i].ToString());
                            else break;
                        }

                        if (parts.Count >= 3)
                            return string.Join(".", parts.GetRange(0, 3));
                    }
                }
            }
            return "192.168.1";
        }

        // Phase 1: ping sweep to populate ARP cache.
        // Phase 2: read ARP table, filter by HP OUI, probe each HP device.
        public async Task ScanAsync(
            string subnet,
            IProgress<(int done, int total, NetworkPrinter? found)> progress,
            CancellationToken ct)
        {
            // Phase 1 — sweep all hosts to populate ARP cache.
            // Ping alone misses printers that have ICMP disabled; a TCP connect
            // attempt still triggers an ARP resolution even when ping is blocked.
            const int total = 254;
            int done = 0;
            var sem = new SemaphoreSlim(48);

            var pings = Enumerable.Range(1, 254).Select(async i =>
            {
                await sem.WaitAsync(ct);
                try
                {
                    ct.ThrowIfCancellationRequested();
                    string ip = $"{subnet}.{i}";
                    // Run ping and TCP probes concurrently; we don't care which
                    // succeeds — any response populates the ARP cache.
                    await Task.WhenAny(
                        PingAsync(ip, ct),
                        CheckPortAsync(ip, 80,   ct),
                        CheckPortAsync(ip, 9100, ct));
                    int d = Interlocked.Increment(ref done);
                    progress.Report((d, total, null));
                }
                finally { sem.Release(); }
            });

            await Task.WhenAll(pings);

            // Phase 2 — read ARP cache and probe HP-OUI devices
            var arp = await ReadArpCacheAsync(subnet, ct);

            foreach (var (mac, ip) in arp)
            {
                ct.ThrowIfCancellationRequested();
                if (!IsHpOui(mac)) continue;

                var printer = await ProbeHostAsync(ip, mac, ct);
                if (printer != null)
                    progress.Report((total, total, printer));
            }
        }

        private static async Task<NetworkPrinter?> ProbeHostAsync(
            string ip, string mac, CancellationToken ct)
        {
            var port9100 = CheckPortAsync(ip, 9100, ct);
            var port80   = CheckPortAsync(ip, 80,   ct);
            var port443  = CheckPortAsync(ip, 443,  ct);
            var port631  = CheckPortAsync(ip, 631,  ct);
            await Task.WhenAll(port9100, port80, port443, port631);

            bool has9100 = port9100.Result;
            bool has80   = port80.Result;
            bool has443  = port443.Result;
            bool has631  = port631.Result;

            string hostname = await ResolveHostnameAsync(ip);

            // Return the printer immediately so it appears in the list,
            // then populate the model in the background.
            var printer = new NetworkPrinter
            {
                IpAddress    = ip,
                MacAddress   = mac,
                Hostname     = hostname,
                Port9100Open = has9100,
                Port80Open   = has80 || has443,
                Port631Open  = has631,
            };

            _ = FetchHpModelAsync(ip, has80, has443, printer, ct).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    printer.Status = $"EWS error: {t.Exception?.InnerException?.Message ?? t.Exception?.Message}";
                else if (string.IsNullOrEmpty(t.Result))
                    printer.Status = "EWS: no model found";
                else
                {
                    printer.Model  = t.Result;
                    printer.Status = "Ready";
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());

            return printer;
        }

        // Read `arp -a` output and return MAC→IP for the given subnet.
        private static async Task<Dictionary<string, string>> ReadArpCacheAsync(
            string subnet, CancellationToken ct)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var psi = new ProcessStartInfo("arp", "-a")
                {
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                };
                using var proc = Process.Start(psi)!;
                string output = await proc.StandardOutput.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);

                // Line format:  "  10.31.41.4          3c-d9-2b-aa-bb-cc     dynamic"
                var re = new Regex(
                    @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\s+([0-9a-f]{2}[-:][0-9a-f]{2}[-:][0-9a-f]{2}[-:][0-9a-f]{2}[-:][0-9a-f]{2}[-:][0-9a-f]{2})",
                    RegexOptions.IgnoreCase);

                foreach (Match m in re.Matches(output))
                {
                    string ip  = m.Groups[1].Value;
                    string mac = m.Groups[2].Value.Replace('-', ':').ToUpperInvariant();
                    if (ip.StartsWith(subnet + ".", StringComparison.Ordinal))
                        map[mac] = ip;
                }
            }
            catch { }
            return map;
        }

        private static bool IsHpOui(string mac)
            => mac.Length >= 8 && HpOuis.Contains(mac[..8]);

        private static async Task<bool> PingAsync(string ip, CancellationToken ct)
        {
            try
            {
                using var ping  = new Ping();
                var reply = await ping.SendPingAsync(ip, 800);
                return reply.Status == IPStatus.Success;
            }
            catch { return false; }
        }

        private static async Task<bool> CheckPortAsync(string ip, int port, CancellationToken ct)
        {
            try
            {
                using var tcp = new TcpClient();
                var connect = tcp.ConnectAsync(ip, port);
                if (await Task.WhenAny(connect, Task.Delay(1500, ct)) != connect) return false;
                return tcp.Connected;
            }
            catch { return false; }
        }

        private static async Task<string> ResolveHostnameAsync(string ip)
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip);
                return entry.HostName;
            }
            catch { return string.Empty; }
        }

        private static async Task<string> FetchHpModelAsync(
            string ip, bool has80, bool has443, NetworkPrinter printer, CancellationToken ct)
        {
            // SNMP is the most reliable method — works even when the EWS page
            // populates the model name via JavaScript (which HTTP scraping can't see).
            printer.Status = "SNMP…";
            string? model = await TrySnmpAsync(ip, ct);
            if (!string.IsNullOrEmpty(model)) return model;

            // Fall back to HTTP/HTTPS XML endpoints.
            var bases = new List<string>();
            if (has80)  bases.Add($"http://{ip}");
            if (has443) bases.Add($"https://{ip}");
            if (bases.Count == 0) { bases.Add($"http://{ip}"); bases.Add($"https://{ip}"); }

            foreach (string baseUrl in bases)
            {
                printer.Status = $"EWS {baseUrl}…";
                model = await TryXmlEndpointAsync(baseUrl, ct);
                if (!string.IsNullOrEmpty(model)) return model;
            }

            printer.Status = "No model found";
            return string.Empty;
        }

        // SNMP v1 GET, community "public". Tries hrDeviceDescr first (actual printer
        // model), falls back to sysDescr (returns generic JetDirect string on HP).
        private static async Task<string?> TrySnmpAsync(string ip, CancellationToken ct)
        {
            // hrDeviceDescr (1.3.6.1.2.1.25.3.2.1.3.1) — the real model name
            byte[] hrDeviceDescr = { 0x2B, 0x06, 0x01, 0x02, 0x01, 0x19, 0x03, 0x02, 0x01, 0x03, 0x01 };
            // sysDescr (1.3.6.1.2.1.1.1.0) — fallback, returns "HP ETHERNET MULTI-ENVIRONMENT" on HP
            byte[] sysDescr      = { 0x2B, 0x06, 0x01, 0x02, 0x01, 0x01, 0x01, 0x00 };

            foreach (byte[] oidBytes in new[] { hrDeviceDescr, sysDescr })
            {
                try
                {
                    byte[] request = BuildSnmpGet(oidBytes);
                    using var udp = new UdpClient();
                    await udp.SendAsync(request, request.Length, ip, 161);

                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    linked.CancelAfter(2000);

                    UdpReceiveResult result;
                    try   { result = await udp.ReceiveAsync(linked.Token); }
                    catch { continue; }

                    string? model = ExtractSnmpString(result.Buffer);
                    if (!string.IsNullOrEmpty(model) &&
                        !model.Contains("ETHERNET MULTI-ENVIRONMENT", StringComparison.OrdinalIgnoreCase))
                        return model;
                }
                catch { }
            }
            return null;
        }

        private static byte[] BuildSnmpGet(byte[] oid)
        {
            byte[] community = System.Text.Encoding.ASCII.GetBytes("public");

            byte[] varBind     = Tlv(0x30, Concat(Tlv(0x06, oid), new byte[] { 0x05, 0x00 }));
            byte[] varBindList = Tlv(0x30, varBind);
            byte[] pdu         = Tlv(0xA0, Concat(
                new byte[] { 0x02, 0x01, 0x01,   // request-id = 1
                             0x02, 0x01, 0x00,   // error-status = 0
                             0x02, 0x01, 0x00 }, // error-index = 0
                varBindList));

            return Tlv(0x30, Concat(
                new byte[] { 0x02, 0x01, 0x00 }, // version = 0 (SNMPv1)
                Tlv(0x04, community),
                pdu));
        }

        // Scan the raw SNMP response for any OctetString containing an HP keyword.
        private static string? ExtractSnmpString(byte[] buf)
        {
            string[] hpKeywords = { "HP", "Hewlett", "LaserJet", "OfficeJet", "DeskJet", "PageWide", "Color Laser" };
            for (int i = 0; i < buf.Length - 2; i++)
            {
                if (buf[i] != 0x04) continue; // OctetString tag
                int len = buf[i + 1];
                if (len > 200 || i + 2 + len > buf.Length) continue;
                string s = System.Text.Encoding.ASCII.GetString(buf, i + 2, len).Trim();
                if (hpKeywords.Any(k => s.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    return s;
            }
            return null;
        }

        private static byte[] TlvEncode(byte tag, byte[] value)
        {
            var r = new List<byte> { tag };
            if (value.Length < 128) { r.Add((byte)value.Length); }
            else                    { r.Add(0x81); r.Add((byte)value.Length); }
            r.AddRange(value);
            return r.ToArray();
        }
        private static byte[] Tlv(byte tag, byte[] value) => TlvEncode(tag, value);

        private static byte[] Concat(params byte[][] parts)
        {
            var r = new List<byte>();
            foreach (var p in parts) r.AddRange(p);
            return r.ToArray();
        }

        private static async Task<string?> TryXmlEndpointAsync(string baseUrl, CancellationToken ct)
        {
            string[] paths = { "/DevMgmt/DiscoveryTree.xml", "/DevMgmt/ProductStatusDyn.xml" };
            foreach (string path in paths)
            {
                try
                {
                    string body = await Http.GetStringAsync(baseUrl + path, ct);
                    var m = Regex.Match(body, @"<[^>]*MakeAndModel[^>]*>([^<]+)<", RegexOptions.IgnoreCase);
                    if (m.Success) return m.Groups[1].Value.Trim();
                    m = Regex.Match(body, @"<[^>]*ProductName[^>]*>([^<]+)<", RegexOptions.IgnoreCase);
                    if (m.Success) return m.Groups[1].Value.Trim();
                }
                catch { }
            }
            return null;
        }
    }
}
