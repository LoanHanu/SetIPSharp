using Renci.SshNet;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {

        string currentHost = "10.99.4.24";
        string username = "ubuntu";
        string password = "ubuntu";
        string netInterface = "eth0"; // or enp0s3
        string netConfigFileName = "";
        string[] lines = new string[0];
        string line;

        using (var ping = new Ping())
        {
            PingReply reply = ping.Send(currentHost);

            if (reply.Status == IPStatus.Success)
            {
                Console.WriteLine("Ping to " + currentHost + " successful. Roundtrip time: " + reply.RoundtripTime + "ms");

                using (var client = new SshClient(currentHost, username, password))
                {
                    client.Connect();

                    if (client.IsConnected)
                    {
                        Console.WriteLine("SSH Connection established.");

                        // run ifconfig to check net state
                        //string result = client.CreateCommand("ifconfig").Execute();
                        //Console.WriteLine($"Current net settings: {Environment.NewLine}{result}");

                        //result = client.CreateCommand("ip a").Execute();
                        //Console.WriteLine($"Current net settings: {Environment.NewLine}{result}");

                        // extract net interface from the result above
                        //if (result.Contains("\r\n"))
                        //{
                        //    lines = result.Split("\r\n");
                        //}
                        //else if (result.Contains("\n"))
                        //{
                        //    lines = result.Split("\n");
                        //}

                        //if (lines.Length > 0)
                        //{
                        //    netInterface = lines[0].Split(":")[0];
                        //}


                        // get network interface name of ethernet:
                        // nmcli device status: get the device status of NetworkManager-controlled interfaces, including their names
                        string result = client.CreateCommand("nmcli device status").Execute();
                        Thread.Sleep(1000);
                        Console.WriteLine($"{Environment.NewLine}Current net state: {Environment.NewLine}{result}");
                        if (result.Contains("\r\n"))
                        {
                            lines = result.Split("\r\n");
                        }
                        else if (result.Contains("\n"))
                        {
                            lines = result.Split("\n");
                        }

                        if (lines.Length > 0)
                        {
                            string[] fields = lines[0].Split(" ", StringSplitOptions.RemoveEmptyEntries);
                            //fields = Regex.Split(lines[0], @"\s+");
                            for (int i = 1; i < lines.Length; i++)
                            {
                                fields = lines[i].Split(" ", StringSplitOptions.RemoveEmptyEntries);
                                //fields = Regex.Split(lines[i], @"\s+");
                                if (fields[1].ToLower() == "ethernet") //&& fields[2].ToLower() == "connected")
                                {
                                    netInterface = fields[0];
                                    Console.WriteLine($"Network Interface: {Environment.NewLine}{netInterface}");
                                    break;
                                }

                            }
                        }

                        // get network config file name with extension ".yaml" : ls /etc/netplan
                        result = client.CreateCommand("ls /etc/netplan").Execute();
                        var names = result.Split(" ", StringSplitOptions.RemoveEmptyEntries).ToList();
                        foreach (var name in names)
                        {
                            string extension = "";
                            extension = Path.GetExtension(name).Trim();
                            if (extension == ".yaml")
                            {
                                netConfigFileName = name;
                                break;
                            }
                        }

                        // run netplan config comman to change with new ip address
                        // Change IP address
                        string newIpAddress = "10.99.4.25"; // Replace with the new IP address you want to assign
                        string gateway = "10.99.4.1";
                        StringBuilder configFileContents = new StringBuilder();
                        configFileContents.AppendLine($"network:");
                        configFileContents.AppendLine($"  version: 2");
                        configFileContents.AppendLine($"  renderer: networkd");
                        configFileContents.AppendLine($"  ethernets:");
                        configFileContents.AppendLine($"    {netInterface}:");
                        configFileContents.AppendLine($"      dhcp4: no");
                        configFileContents.AppendLine($"      addresses: [{newIpAddress}/24]");
                        //configFileContents.AppendLine($"      addresses:");
                        //configFileContents.AppendLine($"        - {newIpAddress}/24");
                        configFileContents.AppendLine($"      gateway4: {gateway}");
                        configFileContents.AppendLine($"      nameservers:");
                        configFileContents.AppendLine($"        addresses: [8.8.8.8, 8.8.4.4]");

                        string netplanConfigCommand = $"echo \"{configFileContents.ToString()}\" | sudo tee /etc/netplan/{netConfigFileName}";

                        var changeIpCommand = client.CreateCommand(netplanConfigCommand);
                        changeIpCommand.Execute();
                        Thread.Sleep(1000);

                        Console.WriteLine("IP address changed to " + newIpAddress);

                        // Reboot the machine
                        var rebootCommand = client.CreateCommand("sudo reboot");
                        try
                        {
                            rebootCommand.Execute();
                            Thread.Sleep(1000);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                        finally
                        {
                            Console.WriteLine("Reboot command executed. The machine will now restart.");

                        }

                    }
                    else
                    {
                        Console.WriteLine("SSH Connection failed.");
                    }

                    client.Disconnect();
                }
            }
            else
            {
                Console.WriteLine("Ping to " + currentHost + " failed. Status: " + reply.Status);
            }
        }



        Console.ReadKey();
    }
}
