using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using vmPing.Classes;

namespace vmPing.UI
{
    public partial class NetworkToolsWindow : Window
    {
        private ObservableCollection<VlsmRequirementViewModel> _vlsmRequirements;

        public NetworkToolsWindow()
        {
            InitializeComponent();
            InitializeVlsmRequirements();
        }

        // ========== CIDR Calculator ==========
        private void CalculateCidr_Click(object sender, RoutedEventArgs e)
        {
            CidrError.Text = "";
            CidrResults.ItemsSource = null;

            string input = CidrInput.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(input))
            {
                CidrError.Text = "请输入 CIDR，例如 192.168.1.0/24";
                return;
            }

            var match = System.Text.RegularExpressions.Regex.Match(input, @"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})/(\d{1,2})$");
            if (!match.Success)
            {
                CidrError.Text = "格式不正确，请输入形如 192.168.1.0/24 的 CIDR";
                return;
            }

            string ip = match.Groups[1].Value;
            if (!int.TryParse(match.Groups[2].Value, out int prefix) || prefix < 0 || prefix > 32)
            {
                CidrError.Text = "CIDR 前缀需为 0-32";
                return;
            }

            var result = CidrCalculator.CalculateIpv4Cidr(ip, prefix);
            if (result == null)
            {
                CidrError.Text = "IP 地址格式不正确";
                return;
            }

            CidrResults.ItemsSource = result.Details;
        }

        // ========== VLSM Planner ==========
        private void InitializeVlsmRequirements()
        {
            _vlsmRequirements = new ObservableCollection<VlsmRequirementViewModel>
            {
                new VlsmRequirementViewModel { Name = "子网 1", Hosts = "50" },
                new VlsmRequirementViewModel { Name = "子网 2", Hosts = "25" },
                new VlsmRequirementViewModel { Name = "子网 3", Hosts = "10" }
            };
            VlsmRequirements.ItemsSource = _vlsmRequirements;
        }

        private void AddVlsmRequirement_Click(object sender, RoutedEventArgs e)
        {
            int count = _vlsmRequirements.Count + 1;
            _vlsmRequirements.Add(new VlsmRequirementViewModel
            {
                Name = $"子网 {count}",
                Hosts = "10"
            });
        }

        private void RemoveVlsmRequirement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is VlsmRequirementViewModel vm)
            {
                _vlsmRequirements.Remove(vm);
            }
        }

        private void CalculateVlsm_Click(object sender, RoutedEventArgs e)
        {
            VlsmError.Text = "";
            VlsmResults.ItemsSource = null;

            string baseCidr = VlsmBaseInput.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(baseCidr))
            {
                VlsmError.Text = "请输入主网段 CIDR";
                return;
            }

            var requirements = new List<VlsmRequirement>();
            foreach (var vm in _vlsmRequirements)
            {
                if (!int.TryParse(vm.Hosts, out int hosts) || hosts < 1)
                {
                    VlsmError.Text = $"\"{vm.Name}\" 的主机数需为不小于 1 的整数";
                    return;
                }
                requirements.Add(new VlsmRequirement { Name = vm.Name, Hosts = hosts });
            }

            var result = CidrCalculator.PlanVlsm(baseCidr, requirements);
            if (!result.Ok)
            {
                VlsmError.Text = result.Message;
                return;
            }

            VlsmResults.ItemsSource = result.Allocations;
        }

        // ========== Subnet Enumeration ==========
        private void EnumerateSubnets_Click(object sender, RoutedEventArgs e)
        {
            EnumError.Text = "";
            EnumResults.ItemsSource = null;

            string baseCidr = EnumBaseInput.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(baseCidr))
            {
                EnumError.Text = "请输入大网段 CIDR";
                return;
            }

            if (!int.TryParse(EnumTargetPrefix.Text?.Trim(), out int targetPrefix) || targetPrefix < 0 || targetPrefix > 32)
            {
                EnumError.Text = "目标前缀需为 0-32";
                return;
            }

            var result = CidrCalculator.EnumerateSubnets(baseCidr, targetPrefix);
            if (!result.Ok)
            {
                EnumError.Text = result.Message;
                return;
            }

            EnumResults.ItemsSource = result.Subnets;
        }

        // ========== IP Converter ==========
        private void ConvertIp_Click(object sender, RoutedEventArgs e)
        {
            ConvertError.Text = "";
            ConvertResults.ItemsSource = null;

            string input = ConvertInput.Text?.Trim() ?? "";
            var result = CidrCalculator.ConvertIpValue(input);

            if (!result.Ok)
            {
                ConvertError.Text = result.Message;
                return;
            }

            var details = new List<CidrDetail>
            {
                new CidrDetail { Label = "来源类型", Value = result.SourceType },
                new CidrDetail { Label = "IPv4 地址", Value = result.Ip },
                new CidrDetail { Label = "十进制", Value = result.Decimal },
                new CidrDetail { Label = "十六进制", Value = result.Hex },
                new CidrDetail { Label = "二进制", Value = result.Binary }
            };

            ConvertResults.ItemsSource = details;
        }
    }

    public class VlsmRequirementViewModel
    {
        public string Name { get; set; }
        public string Hosts { get; set; }
    }
}
