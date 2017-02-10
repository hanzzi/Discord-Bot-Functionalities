using Discord.Commands;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Discord_Bot_Functionalities
{
    class Rejseplanen : DiscordBotFunctions
    {
        // TODO: Make constructors for input.

        private string BaseUrl = "http://xmlopen.rejseplanen.dk/bin/rest.exe";
        private bool _originDone;
        private bool _destinationDone;
        private bool _planTripDone;

        private WebClient _WebClient = new WebClient();

        // searches for the input given by the user
        public async Task UserInputSearch(string userInput, CommandEventArgs e)
        {
            _WebClient.BaseAddress = BaseUrl;
            _WebClient.Encoding = Encoding.UTF8;

            Uri uri = new Uri($"{BaseUrl}/location?input={userInput}");

            // Downloads the string and stacks it in neat rows when migrating to 1.0 consider making this with a embedbuilder
            _WebClient.DownloadStringCompleted += async (s, m) =>
            {
                string response = m.Result;

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(response);

                XmlElement root = doc.DocumentElement;
                XmlNodeList stopLocations = root.SelectNodes("//LocationList/StopLocation");
                StringBuilder sb = new StringBuilder();
                sb.Capacity = 2000;
                sb.Append("```");

                // TODO: Replace Break Function with stringbuilder buffer which works as a character counter. MIGHT NOT WORK
                foreach (XmlNode node in stopLocations)
                {
                    sb.Append(node.Attributes[0].OuterXml + Environment.NewLine);
                    sb.Append(node.Attributes[1].OuterXml + Environment.NewLine);
                    sb.Append(node.Attributes[2].OuterXml + Environment.NewLine);
                    sb.Append(node.Attributes[3].OuterXml + Environment.NewLine);
                    sb.Append("-----------------------" + Environment.NewLine);
                    if (sb.Length > 1890)
                    {
                        break;
                    }
                }
                sb.Append("```");
                await e.Channel.SendMessage(sb.ToString());
            };
            _WebClient.DownloadStringAsync(uri);

        }

        // gets the starting point
        public async Task GetOrigin(CommandEventArgs e, string origin, string destination, int iterations)
        {
            _WebClient.BaseAddress = BaseUrl;
            _WebClient.Encoding = System.Text.Encoding.UTF8;

            Uri originURI = new Uri($"{BaseUrl}/location?input={origin}");

            string OriginName;
            string OriginX;
            string OriginY;
            string OriginID = null;

            // Subscribes to the event when the webclient has finished loading a page
            _WebClient.DownloadStringCompleted += async (s, m) =>
            {
                if (_originDone != true)
                {
                    string response = m.Result;

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(response);

                    XmlElement root = doc.DocumentElement;
                    XmlNodeList stopLocation = root.SelectNodes("//LocationList/StopLocation");
                    XmlNodeList coordLocation = root.SelectNodes("//LocationList/CoordLocation");


                    // Gets the Attributes if the current string is of the type stoplocation
                    if (root.FirstChild.Name == "StopLocation")
                    {
                        OriginID = stopLocation.Item(0).Attributes[3].Value;
                    }
                    // Gets the Attributes if the current string is of the type coordlocation
                    if (root.FirstChild.Name == "CoordLocation")
                    {
                        try
                        {
                            if (stopLocation != null)
                            {
                                // If the first Location is a CoordLocation get the first StopLocation 
                                OriginID = stopLocation.Item(0).Attributes[3].Value;
                            }
                            else
                            {
                                await e.Channel.SendMessage("Origin Returned Null try again");
                            }
                        }
                        // if the query gets nothing an indexoutofrangeexeption is thrown and an error message is sent
                        catch (IndexOutOfRangeException rangeEx)
                        {
                            await e.Channel.SendMessage("Query failed try again");
                            await e.Channel.SendMessage(rangeEx.ToString());
                        }
                    }
                    _originDone = true;
                    await GetDestination(e, destination, OriginID, iterations);
                }
            };
            _WebClient.DownloadStringAsync(originURI);



        }

        // gets the destination
        private async Task GetDestination(CommandEventArgs e, string Destination, string OriginID, int Iterations)
        {
            _WebClient.BaseAddress = BaseUrl;

            Uri DestURI = new Uri($"{BaseUrl}/location?input={Destination}");

            string DestName = null;
            string DestX = null;
            string DestY = null;
            string DestID = null;

            // Subscribes to the event when the webclient has finished loading a page
            _WebClient.DownloadStringCompleted += async (s, m) =>
            {
                if (_destinationDone != true)
                {
                    string Response = m.Result;

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(Response);

                    XmlElement root = doc.DocumentElement;
                    XmlNodeList StopLocation = root.SelectNodes("//LocationList/StopLocation");
                    XmlNodeList CoordLocation = root.SelectNodes("//LocationList/CoordLocation");

                    if (root.FirstChild.Name == "StopLocation")
                    {
                        if (StopLocation != null)
                        {
                            DestName = StopLocation.Item(0).Attributes[0].Value;
                            DestX = StopLocation.Item(0).Attributes[1].Value;
                            DestY = StopLocation.Item(0).Attributes[2].Value;
                            DestID = StopLocation.Item(0).Attributes[3].Value;
                        }
                        else
                        {
                            await e.Channel.SendMessage("Destination returned null try again");
                        }

                    }
                    if (root.FirstChild.Name == "CoordLocation")
                    {
                        try
                        {
                            if (CoordLocation != null)
                            {
                                DestName = CoordLocation.Item(0).Attributes[0].Value;
                                DestX = CoordLocation.Item(0).Attributes[1].Value;
                                DestY = CoordLocation.Item(0).Attributes[2].Value;
                                DestID = CoordLocation.Item(0).Attributes[3].Value;
                            }
                            else
                            {
                                await e.Channel.SendMessage("Destination returned null try again");
                            }
                        }
                        catch (IndexOutOfRangeException RangeEx)
                        {
                            await e.Channel.SendMessage("Query failed try again");
                            await e.Channel.SendMessage(RangeEx.ToString());
                        }
                    }
                    _destinationDone = true;
                    await PlanTrip(OriginID, DestX, DestY, DestName, e, Iterations);
                }
            };
            _WebClient.DownloadStringAsync(DestURI);
        }

        // queries the API for the trip
        private async Task PlanTrip(string OriginID, string DestCoordX, string DestCoordY, string DestCoordName, CommandEventArgs e, int Iterations)
        {
            try
            {
                _WebClient.BaseAddress = BaseUrl;

                Uri URI = new Uri($"{BaseUrl}/trip?originId={OriginID}&destCoordX={DestCoordX}&destCoordY={DestCoordY}&destCoordName={DestCoordName}"/*&date={Date}&time={Time}"*/);

                _WebClient.DownloadStringCompleted += async (s, m) =>
                {
                    if (_planTripDone != true)
                    {
                        string Response = m.Result;

                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(Response);

                        XmlElement root = doc.DocumentElement;
                        XmlNodeList Trip = root.SelectNodes("//Trip");
                        XmlNodeList Legs = root.SelectNodes("//Trip/Leg");
                        XmlNodeList Origin = root.SelectNodes("//Trip/Leg/Origin");
                        XmlNodeList Destination = root.SelectNodes("//Trip/Leg/Destination");
                        StringBuilder sb = new StringBuilder();


                        int Tripcount = 1;

                        try
                        {

                            foreach (XmlNode Node in Trip)
                            {
                                sb.Append("```");
                                sb.Append($"OPTION: {Tripcount}{Environment.NewLine}");

                                for (int i = 0; i < Node.ChildNodes.Count; i++)
                                {
                                    if (Node.ChildNodes.Item(i).Attributes[1].Value == "IC" | Node.ChildNodes.Item(i).Attributes[1].Value == "ICL" | Node.ChildNodes.Item(i).Attributes[1].Value == "LYN")
                                    {
                                        // TRAIN FORMAT
                                        sb.Append($"Train: {Node.ChildNodes.Item(i).Attributes[0].Value.ToString()}{Environment.NewLine}");
                                        sb.Append($"From: {Node.ChildNodes.Item(i).ChildNodes.Item(0).Attributes.Item(0).Value}{Environment.NewLine}");
                                        sb.Append($"Departure: {Node.ChildNodes.Item(i).ChildNodes.Item(0).Attributes.Item(3).Value}{Environment.NewLine}");
                                        sb.Append($"To: {Node.ChildNodes.Item(i).ChildNodes.Item(1).Attributes.Item(0).Value}{Environment.NewLine}");
                                        sb.Append($"Arrival: {Node.ChildNodes.Item(i).ChildNodes.Item(1).Attributes.Item(3).Value}{Environment.NewLine}");
                                        sb.Append($"Date: {Node.ChildNodes.Item(i).ChildNodes.Item(0).Attributes.Item(4).Value}{Environment.NewLine}");
                                        sb.Append($"Track: {Node.ChildNodes.Item(i).ChildNodes.Item(0).Attributes.Item(5).Value}{Environment.NewLine}");
                                        sb.Append($"-------------------{Environment.NewLine}");
                                    }
                                    if (Node.ChildNodes.Item(i).Attributes[1].Value.ToString() == "BUS")
                                    {
                                        // BUS FORMAT
                                        sb.Append($"Bus: {Node.ChildNodes.Item(i).Attributes[0].Value.ToString()}{Environment.NewLine}");
                                        sb.Append($"From: {Node.ChildNodes.Item(i).ChildNodes.Item(0).Attributes.Item(0).Value}{Environment.NewLine}");
                                        sb.Append($"Departure: {Node.ChildNodes.Item(i).ChildNodes.Item(0).Attributes.Item(3).Value}{Environment.NewLine}");
                                        sb.Append($"To: {Node.ChildNodes.Item(i).ChildNodes.Item(1).Attributes.Item(0).Value}{Environment.NewLine}");
                                        sb.Append($"Arrival: {Node.ChildNodes.Item(i).ChildNodes.Item(1).Attributes.Item(3).Value}{Environment.NewLine}");
                                        sb.Append($"Date: {Node.ChildNodes.Item(i).ChildNodes.Item(0).Attributes.Item(4).Value}{Environment.NewLine}");
                                        sb.Append($"-------------------{Environment.NewLine}");
                                    }
                                    if (Node.ChildNodes.Item(i).Attributes[1].Value.ToString() == "WALK")
                                    {
                                        sb.Append($"WALK{Environment.NewLine}");
                                        sb.Append($"From: {Node.ChildNodes.Item(i).ChildNodes.Item(0).Attributes.Item(0).Value}{Environment.NewLine}");
                                        sb.Append($"To: {Node.ChildNodes.Item(i).ChildNodes.Item(1).Attributes.Item(0).Value}{Environment.NewLine}");
                                        sb.Append($"{Node.ChildNodes.Item(i).ChildNodes.Item(2).Attributes.Item(0).Value.Replace(";", string.Empty).Replace("Varighed", "Duration").Replace("Afstand", "Distance")}{Environment.NewLine}");
                                        sb.Append($"-------------------{Environment.NewLine}");
                                    }

                                }
                                sb.Append("```");
                                await e.Channel.SendMessage(sb.ToString());
                                sb.Clear();
                                Tripcount++;
                                if (Tripcount > Iterations)
                                    break;
                            }
                            Tripcount = 1;
                        }
                        catch (Exception)
                        {
                            await e.Channel.SendMessage("Query Failed");
                        }
                    }
                    _planTripDone = true;
                };
                _WebClient.DownloadStringAsync(URI);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }
}
