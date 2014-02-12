using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RemoteTech
{
    public class TimeQuadrantPatcher
    {
        private class Backup
        {
            public TimeWarp TimeQuadrant { get; set; }
            public Texture Texture { get; set; }
            public Vector3 Scale { get; set; }
            public Vector3 Center { get; set; }
            public Vector3 ExpandedPosition { get; set; }
            public Vector3 CollapsedPosition { get; set; }
        }

        private static readonly GUIStyle mFlightButtonGreen, mFlightButtonRed, mFlightButtonYellow;

        private String DisplayText
        {
            get
            {
                var vs = RTCore.Instance.Satellites[FlightGlobals.ActiveVessel];
                if (vs == null)
                {
                    return "N/A";
                }
                else if (vs.HasLocalControl)
                {
                    return "Local Control";
                }
                else if (vs.Connections.Any())
                {
                    if (RTSettings.Instance.EnableSignalDelay)
                    {
                        return "D+ " + vs.Connections[0].Delay.ToString("F6") + "s";
                    }
                    else
                    {
                        return "Connected";
                    }                    
                }
                return "No Connection";
            }
        }

        private GUIStyle ButtonStyle
        {
            get
            {
                var vs = RTCore.Instance.Satellites[FlightGlobals.ActiveVessel];
                if (vs == null) 
                {
                    return mFlightButtonRed;
                }
                else if (vs.HasLocalControl)
                {
                    return mFlightButtonYellow;
                }
                else if (vs.Connections.Any())
                {
                    return mFlightButtonGreen;
                }
                return mFlightButtonRed;
            }
        }

        private Backup mBackup;
        private GUIStyle mTextStyle;
        private GUIStyle mGreenTextStyle;
        private GUIStyle mRedTextStyle;
        private GUIStyle mTargetTextStyle;
        private bool mDisplayRoute;

        static TimeQuadrantPatcher()
        {
            mFlightButtonGreen = GUITextureButtonFactory.CreateFromFilename("texFlightGreen.png",
                                                                       "texFlightGreenOver.png",
                                                                       "texFlightGreenDown.png",
                                                                       "texFlightGreenOver.png");
            mFlightButtonYellow = GUITextureButtonFactory.CreateFromFilename("texFlightYellow.png",
                                                                       "texFlightYellowOver.png",
                                                                       "texFlightYellowDown.png",
                                                                       "texFlightYellowOver.png");
            mFlightButtonRed = GUITextureButtonFactory.CreateFromFilename("texFlightRed.png",
                                                                       "texFlightRed.png",
                                                                       "texFlightRed.png",
                                                                       "texFlightRed.png");
            mFlightButtonGreen.fixedHeight = mFlightButtonGreen.fixedWidth = 0;
            mFlightButtonYellow.fixedHeight = mFlightButtonYellow.fixedWidth = 0;
            mFlightButtonRed.fixedHeight = mFlightButtonRed.fixedWidth = 0;
            mFlightButtonGreen.stretchHeight = mFlightButtonGreen.stretchWidth = true;
            mFlightButtonYellow.stretchHeight = mFlightButtonYellow.stretchWidth = true;
            mFlightButtonRed.stretchHeight = mFlightButtonRed.stretchWidth = true;
        }

        public void Patch()
        {
            var timeQuadrant = TimeWarp.fetch;
            if (timeQuadrant == null) return;
            if (mBackup != null)
            {
                throw new InvalidOperationException("Patcher is already in use.");
            }
            var tab = timeQuadrant.timeQuadrantTab;
            mBackup = new Backup()
            {
                TimeQuadrant = timeQuadrant,
                Texture = tab.renderer.material.mainTexture,
                Scale = tab.transform.localScale,
                Center = ((BoxCollider)tab.collider).center,
                ExpandedPosition = tab.expandedPos,
                CollapsedPosition = tab.collapsedPos,
            };

            List<Transform> children = new List<Transform>();

            foreach (Transform child in tab.transform)
            {
                children.Add(child);
            }

            foreach (Transform child in children)
            {
                child.parent = tab.transform.parent;
            }

            // Set the new texture
            float old_height = tab.renderer.material.mainTexture.height;
            Texture2D newTexture;
            RTUtil.LoadImage(out newTexture, "texTimeQuadrant.png");
            newTexture.filterMode = FilterMode.Trilinear;
            newTexture.wrapMode = TextureWrapMode.Clamp;
            tab.renderer.material.mainTexture = newTexture;

            // Apply new scale, positions
            float scale = Screen.height / (float)GameSettings.UI_SIZE;
            tab.transform.localScale = new Vector3(tab.transform.localScale.x,
                                                   tab.transform.localScale.y,
                                                   tab.transform.localScale.z * (tab.renderer.material.mainTexture.height / old_height));
            tab.collapsedPos += new Vector3(0, -0.013f, 0);
            tab.expandedPos += new Vector3(0, -0.013f, 0);
            foreach (Transform child in children)
            {
                child.localPosition += new Vector3(0, 0.013f, 0);
            }
            tab.Expand();

            foreach (Transform child in children)
            {
                child.parent = tab.transform;
            }

            ((BoxCollider)tab.collider).center += new Vector3(0, 0, -0.37f);

            var text = tab.transform.FindChild("MET timer").GetComponent<ScreenSafeGUIText>();
            mTextStyle = new GUIStyle(text.textStyle);
            mTextStyle.fontSize = (int)(text.textSize * ScreenSafeUI.PixelRatio);

            mGreenTextStyle = new GUIStyle(mTextStyle);
            mGreenTextStyle.normal.textColor = Color.green;
            mGreenTextStyle.alignment = TextAnchor.LowerRight;

            mRedTextStyle = new GUIStyle(mTextStyle);
            mRedTextStyle.normal.textColor = Color.red;
            mRedTextStyle.alignment = TextAnchor.LowerRight;

            mTargetTextStyle = new GUIStyle(mTextStyle);
            mTargetTextStyle.normal.textColor = Color.green;
            mTargetTextStyle.alignment = TextAnchor.LowerLeft;


            mDisplayRoute = false;

            RenderingManager.AddToPostDrawQueue(0, Draw);
        }

        public void Undo()
        {
            try
            {
                RenderingManager.RemoveFromPostDrawQueue(0, Draw);

                if (mBackup == null)
                    return;

                var tab = mBackup.TimeQuadrant.timeQuadrantTab;

                if (tab.collider != null)
                {
                    ((BoxCollider)tab.collider).center = mBackup.Center;
                }

                List<Transform> children = new List<Transform>();

                foreach (Transform child in tab.transform)
                {
                    children.Add(child);
                    child.parent = tab.transform.parent;
                }

                tab.transform.localScale = mBackup.Scale;
                tab.expandedPos = mBackup.ExpandedPosition;
                tab.collapsedPos = mBackup.CollapsedPosition;
                foreach (Transform child in children)
                {
                    child.localPosition += new Vector3(0, -0.013f, 0);
                }
                tab.Collapse();
                tab.renderer.material.mainTexture = mBackup.Texture;

                foreach (Transform child in children)
                {
                    child.parent = tab.transform;
                }

                mBackup = null;
            }
            catch (Exception) { }
        }

        private void DrawSignalRoute()
        {
            float scale = ScreenSafeUI.VerticalRatio * 900.0f / Screen.height;
            Vector2 screenCoord = ScreenSafeUI.referenceCam.WorldToScreenPoint(mBackup.TimeQuadrant.timeQuadrantTab.transform.position);

            float distanceWidth = 80;
            float antennaWidth = 40;
            float targetWidth = 250;
            float areaWidth = distanceWidth + antennaWidth + antennaWidth + targetWidth;
            float lineHeight = 20;

            var satellite = RTCore.Instance.Satellites[FlightGlobals.ActiveVessel];
            var source = satellite as ISatellite;
            NetworkRoute<ISatellite> connection;

            // If there are no connections, or an infinite delay connection, examine the LastConnection
            // other wise use the currently shortest connection
            if (RTCore.Instance.Network[satellite].Count == 0 ||
                Double.IsPositiveInfinity(RTCore.Instance.Network[satellite][0].Delay))
                connection = RTCore.Instance.Network.LastConnection(satellite);
            else
                connection = RTCore.Instance.Network[satellite][0];

            if(connection == null)
                GUILayout.BeginArea(new Rect(5.0f / scale, Screen.height - screenCoord.y + 38.0f / scale, areaWidth, lineHeight * 2));
            else
                GUILayout.BeginArea(new Rect(5.0f / scale, Screen.height - screenCoord.y + 38.0f / scale, areaWidth, lineHeight * (connection.Links.Count + 1)));

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Distance", mGreenTextStyle,  GUILayout.Width(distanceWidth));
            GUILayout.Label("TX",       mGreenTextStyle,  GUILayout.Width(antennaWidth));
            GUILayout.Label("RX",       mGreenTextStyle,  GUILayout.Width(antennaWidth));
            GUILayout.Space(10);
            GUILayout.Label("Target",   mTargetTextStyle, GUILayout.Width(targetWidth));
            GUILayout.EndHorizontal();

            if (connection != null && connection.Links.Count > 0)
            {
                foreach (var link in connection.Links)
                {
                    GUILayout.BeginHorizontal();

                    // Distance
                    if (source.DistanceTo(link.Target) > Math.Min(link.Transmitters.Max(a => (a.Omni != 0) ? a.Omni : a.Dish), 
                        link.Receivers.Max(a => (a.Omni != 0) ? a.Omni : a.Dish)))
                        GUILayout.Label(RTUtil.FormatSI(source.DistanceTo(link.Target), "m"),   mRedTextStyle, GUILayout.Width(distanceWidth));
                    else
                        GUILayout.Label(RTUtil.FormatSI(source.DistanceTo(link.Target), "m"), mGreenTextStyle, GUILayout.Width(distanceWidth));

                    // Transmitter status
                    // TODO: rework for non-Standard range models
                    if (!source.Antennas.Any(a => a.Powered && a.Activated))
                        GUILayout.Label("OFF",  mRedTextStyle, GUILayout.Width(antennaWidth));
                    else if (link.Transmitters.Max(a => (a.Omni != 0) ? a.Omni : a.Dish) < source.DistanceTo(link.Target))
                        GUILayout.Label("RNG",  mRedTextStyle, GUILayout.Width(antennaWidth));
                    else if (!source.HasLineOfSightWith(link.Target))
                        GUILayout.Label("LOS",  mRedTextStyle, GUILayout.Width(antennaWidth));
                    else if (!link.Transmitters.Any(a => RangeModelExtensions.IsTargeting(a, link.Target, source)))
                        GUILayout.Label("AIM",  mRedTextStyle, GUILayout.Width(antennaWidth));
                    else
                        GUILayout.Label("OK", mGreenTextStyle, GUILayout.Width(antennaWidth));

                    // Receiver status
                    // TODO: rework for non-Standard range models
                    if (!link.Target.Antennas.Any(a => a.Powered && a.Activated))
                        GUILayout.Label("OFF",  mRedTextStyle, GUILayout.Width(antennaWidth));
                    else if (link.Receivers.Max(a => (a.Omni != 0) ? a.Omni : a.Dish) < link.Target.DistanceTo(source))
                        GUILayout.Label("RNG",  mRedTextStyle, GUILayout.Width(antennaWidth));
                    else if (!link.Target.HasLineOfSightWith(source))
                        GUILayout.Label("LOS",  mRedTextStyle, GUILayout.Width(antennaWidth));
                    else if (!link.Receivers.Any(a => RangeModelExtensions.IsTargeting(a, source, link.Target)))
                        GUILayout.Label("AIM",  mRedTextStyle, GUILayout.Width(antennaWidth));
                    else
                        GUILayout.Label("OK", mGreenTextStyle, GUILayout.Width(antennaWidth));

                    GUILayout.Space(10);

                    // Target of link
                    GUILayout.Label(link.Target.Name, mTargetTextStyle, GUILayout.Width(targetWidth));

                    source = link.Target;
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                // No connection information available
                GUILayout.BeginHorizontal();
                GUILayout.Label("No Connection Information", mGreenTextStyle, GUILayout.Width(areaWidth));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        public void Draw()
        {
            if (mBackup != null)
            {
                float scale = ScreenSafeUI.VerticalRatio * 900.0f / Screen.height;
                Vector2 screenCoord = ScreenSafeUI.referenceCam.WorldToScreenPoint(mBackup.TimeQuadrant.timeQuadrantTab.transform.position);
                Rect screenPos = new Rect(5.0f / scale, Screen.height - screenCoord.y + 14.0f / scale, 50.0f / scale, 20.0f / scale);

                if (GUI.Button(screenPos, DisplayText, mTextStyle))
                {
                    mDisplayRoute = !mDisplayRoute;
                }

                screenPos.width = 21.0f / scale;
                screenPos.x += 101 / scale;

                if (GUI.Button(screenPos, "", ButtonStyle))
                {
                    var satellite = RTCore.Instance.Satellites[FlightGlobals.ActiveVessel];
                    if (satellite == null || satellite.SignalProcessor.FlightComputer == null) return;
                    satellite.SignalProcessor.FlightComputer.Window.Show();
                    //ScreenMessages.PostScreenMessage(new ScreenMessage("[FlightComputer]: Not yet implemented!", 4.0f, ScreenMessageStyle.UPPER_LEFT));
                }

                if (mDisplayRoute)
                    DrawSignalRoute();
            }
        }
    }
}