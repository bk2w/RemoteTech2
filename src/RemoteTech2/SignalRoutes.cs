using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RemoteTech
{
    public class SignalRoutes : ScenarioModule
    {
        private SignalRoutes()
        {
        }

        public static SignalRoutes Current {
            get
            {
                var game = HighLogic.CurrentGame;
                if (game == null) { return null; }

                if (!game.scenarios.Any(p => p.moduleName == typeof(SignalRoutes).Name))
                {
                    var proto = game.AddProtoScenarioModule(typeof(SignalRoutes), GameScenes.FLIGHT, GameScenes.TRACKSTATION);
                    if (proto.targetScenes.Contains(HighLogic.LoadedScene))
                    {
                        proto.Load(ScenarioRunner.fetch);
                    }
                }

                return game.scenarios.Select(s => s.moduleRef).OfType<SignalRoutes>().SingleOrDefault();
            }
        }

        public List<NetworkRoute<ISatellite>> this[ISatellite sat]
        {
            get
            {
                if (sat == null)
                    return new List<NetworkRoute<ISatellite>>();
                return mSignalRoutes.ContainsKey(sat) ? mSignalRoutes[sat] : new List<NetworkRoute<ISatellite>>();
            }
            set
            {
                mSignalRoutes[sat] = value;
            }
        }

        public void Remove(ISatellite sat)
        {
            mSignalRoutes.Remove(sat);
        }

        public Dictionary<ISatellite, List<NetworkRoute<ISatellite>>> mSignalRoutes = new Dictionary<ISatellite, List<NetworkRoute<ISatellite>>>();

        public override void OnLoad(ConfigNode configNode)
        {
            RTLog.Notify("SignalRoutes::OnLoad started");

            //Do we have the right version? If not, error out
            // Are there any Satellite nodes? If not, we're done
            // For each Satellite node:
            //  Does the satellite exist? If not, go to the next Satellite node
            //  Does the Satellite Node have any Route nodes? If not, go to the next Satellite node
            //  For each Route Node:
            //   Does the goal exist? If not, go to the next Route node
            //   Does the route have a non-negative delay? If no, go to the next Route node
            //   Does the route have any links? If not, go to the next Route node
            //   For each Link Node:
            //    Does the link have a non-negative delay? If not, go to the next Route node
            //    Does the link target exist? If not, go to the next Route node
            //    Is the link type Dish or Omni? If not, go to the next Route node
            //    Does the link have Transmitter and Receiver nodes? If not, go to the next Route node
            //    For each Transmitter/Receiver node:
            //     Does the part id exist on the satellite? If not, go to the next Route node
            //     Add the transmitter/receiver to the link antennas
            //    Add the link to the route
            //   Add the route to the cache

            // Is there a version?
            if (!configNode.HasValue("Version") )
            {
                RTLog.Notify("No SignalRoutes version, skipping all data");
                return;
            }

            // Is the version correct?
            if(configNode.GetValue("Version") != "1")
            {
                RTLog.Notify("Unexpected SignalRoutes version " + configNode.GetValue("Version") + ", skipping all data");
                return;
            }

            // Are there any Satellite nodes?
            if (!configNode.HasNode("Satellite"))
            {
                RTLog.Notify("SignalRoutes data has no satellites, skipping all data");
                return;
            }

            Debug.Log("SignalRoutes::OnLoad - " + "configNode has "+ configNode.CountNodes + " nodes");

            foreach (var satNode in configNode.GetNodes("Satellite"))
            {
                // Does the node have a GUID? 
                if (!satNode.HasValue("Guid"))
                {
                    RTLog.Notify("Satellite node is missing Guid, skipping to next satellite");
                    continue;
                }

                var satGuid = new Guid(satNode.GetValue("Guid"));
                var sat = RTCore.Instance.Satellites[satGuid] as ISatellite;

                // Does the satellite exist?
                if (sat == null)
                {
                    RTLog.Notify("Satellite " + satGuid + " doesn't exist, skipping to next satellite");
                    continue;
                }

                // Does the node have any routes?
                if (!satNode.HasNode("Route"))
                {
                    RTLog.Notify("Satellite " + sat.Name + " has no route data, skipping to next satellite");
                    continue;
                }

                RTLog.Notify("Scanning route data for satellite " + sat.Name);
                var routeList = new List<NetworkRoute<ISatellite>>();
                foreach (var routeNode in satNode.GetNodes("Route"))
                {
                    // Does the goal exist?
                    if (!routeNode.HasValue("Goal"))
                    {
                        RTLog.Notify("Route has no goal, skipping to next route");
                        continue;
                    }

                    var goalGuid = new Guid(routeNode.GetValue("Goal"));
                    var goal = RTCore.Instance.Satellites[goalGuid] ?? RTCore.Instance.Network.GroundStations[goalGuid];
                    if (goal == null)
                    {
                        RTLog.Notify("Route goal " + goalGuid + " doesn't exist, skipping to next route");
                        continue;
                    }

                    // Does the route have a non-negative delay?
                    if (!routeNode.HasValue("Delay"))
                    {
                        RTLog.Notify("Route has no delay, skipping to next route");
                        continue;
                    }
                    var delay = Convert.ToDouble(routeNode.GetValue("Delay"));
                    if (delay < 0)
                    {
                        RTLog.Notify("Route has negative delay of " + delay + ", skipping to next route");
                        continue;
                    }

                    // Does the route have any links?
                    if (!routeNode.HasNode("Link"))
                    {
                        RTLog.Notify("Route has no link data, skipping to next route");
                        continue;
                    }

                    // Note down the starting satellite
                    var source = sat;

                    var linkList = new List<NetworkLink<ISatellite>>();

                    foreach (var linkNode in routeNode.GetNodes("Link"))
                    {
                        // Does the link have a non-negative cost?
                        if (!linkNode.HasValue("Cost"))
                        {
                            RTLog.Notify("Link has no cost, skipping to next link");
                            continue;
                        }
                        var cost = Convert.ToDouble(linkNode.GetValue("Cost"));
                        if (cost < 0)
                        {
                            RTLog.Notify("Link has negative cost of " + cost + ", skipping to next link");
                            continue;
                        }

                        // Does the link target exist?
                        if (!linkNode.HasValue("Target Guid"))
                        {
                            RTLog.Notify("Link has no target guid, skipping to next link");
                            continue;
                        }
                        var targetGuid = new Guid(linkNode.GetValue("Target Guid"));

                        var target = RTCore.Instance.Satellites[targetGuid] as ISatellite;
                        if (target == null && !RTCore.Instance.Network.GroundStations.TryGetValue(targetGuid, out target))
                        {
                            RTLog.Notify("Link target " + targetGuid + " doesn't exist, skipping to next link");
                            continue;
                        }
//                        RTLog.Notify("Link target is " + target.Name);

                        // Is the link type Dish or Omni?
                        if (!linkNode.HasValue("LinkType"))
                        {
                            RTLog.Notify("Link has no link type, skipping to next link");
                            continue;
                        }
                        var linkType = linkNode.GetValue("LinkType");
                        if (linkType != "Omni" && linkType != "Dish")
                        {
                            RTLog.Notify("Link type " + linkType + " is not recognized, skipping to next link");
                            continue;
                        }
                        var port = (linkType == "Dish") ? LinkType.Dish : LinkType.Omni;

                        // Does the link have Transmitter and Receiver nodes?
                        if (!linkNode.HasNode("Transmitter"))
                        {
                            RTLog.Notify("Link has no transmitters, skipping to next link");
                            continue;
                        }
                        else RTLog.Notify("Link has transmitters");
                        if (!linkNode.HasNode("Receiver"))
                        {
                            RTLog.Notify("Link has no receivers, skipping to next link");
                            continue;
                        }
//                        else RTLog.Notify("Link has receivers");
                        var txList = new List<IAntenna>();
                        foreach (var txNode in linkNode.GetNodes("Transmitter"))
                        {
                            // Does the part id exist on the source satellite?
                            if (!txNode.HasValue("Part ID"))
                            {
                                RTLog.Notify("Transmitter has no part id, skipping to next transmitter");
                                continue;
                            }

                            var partId = Convert.ToUInt32(txNode.GetValue("Part ID"));
                            var txAntenna = source.Antennas.Where(a => a.PartId == partId).FirstOrDefault();

                            if (txAntenna == null)
                            {
                                RTLog.Notify("Transmitter doesn't exist on source satellite, skipping to next transmitter");
                                continue;
                            }

                            txList.Add(txAntenna);
                        } // foreach transmitter

                        var rxList = new List<IAntenna>();
                        foreach (var rxNode in linkNode.GetNodes("Receiver"))
                        {
                            // Does the part id exist on the target satellite?
                            if (!rxNode.HasValue("Part ID"))
                            {
                                RTLog.Notify("Receiver has no part id, skipping to next receiver");
                                continue;
                            }

                            var partId = Convert.ToUInt32(rxNode.GetValue("Part ID"));
                            var rxAntenna = target.Antennas.Where(a => a.PartId == partId).FirstOrDefault();

                            if (rxAntenna == null)
                            {
                                RTLog.Notify("Receiver doesn't exist on target satellite, skipping to next receiver");
                                continue;
                            }

                            rxList.Add(rxAntenna);
                        } // foreach receiver

                        linkList.Add(new NetworkLink<ISatellite>(target, txList, rxList, port, cost));
                        source = target;
                    } // foreach link

                    var route = new NetworkRoute<ISatellite>(sat, linkList, delay);
                    RTLog.Notify("Loaded route from " + sat.Name + " to " + linkList[linkList.Count-1].Target.Name);
                    RTLog.Notify(route.ToString());
                    routeList.Add(route);
                } // foreach route

                mSignalRoutes[sat] = routeList;
            } // foreach satellite

            RTLog.Notify("SignalRoutes::OnLoad finished");
        }

        public override void OnSave(ConfigNode configNode)
        {
            RTLog.Notify("SignalRoutes::OnSave started");
            configNode.AddValue("Version", "1");

            foreach (var sat in mSignalRoutes.Keys.Where(a => (mSignalRoutes[a] != null)))
            {
                var satNode = new ConfigNode("Satellite");
                satNode.AddValue("Name", sat.Name);
                satNode.AddValue("Guid", sat.Guid.ToString());

                foreach (var route in mSignalRoutes[sat])
                {
                    var routeNode = new ConfigNode("Route");
                    routeNode.AddValue("Delay", route.Delay);
                    routeNode.AddValue("Goal", route.Goal.Guid.ToString());

                    foreach (var link in route.Links)
                    {
                        var linkNode = new ConfigNode("Link");

                        linkNode.AddValue("Cost", link.Cost);
                        linkNode.AddValue("LinkType", link.Port);
                        linkNode.AddValue("Target Name", link.Target.Name);
                        linkNode.AddValue("Target Guid", link.Target.Guid.ToString());

                        foreach (var tx in link.Transmitters)
                        {
                            var txNode = new ConfigNode("Transmitter");
                            txNode.AddValue("Name", tx.Name);
                            txNode.AddValue("Guid", tx.Guid.ToString());
                            txNode.AddValue("Part ID", tx.PartId);
                            linkNode.AddNode(txNode);
                        }

                        foreach (var rx in link.Receivers)
                        {
                            var rxNode = new ConfigNode("Receiver");
                            rxNode.AddValue("Name", rx.Name);
                            rxNode.AddValue("Guid", rx.Guid.ToString());
                            rxNode.AddValue("Part ID", rx.PartId);
                            linkNode.AddNode(rxNode);
                        }
                        routeNode.AddNode(linkNode);
                    }
                    satNode.AddNode(routeNode);
                }
                configNode.AddNode(satNode);
            }

            RTLog.Notify("SignalRoutes::OnSave finished");
        }
    }
}