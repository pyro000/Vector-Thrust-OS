using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        #region WhipsHorizon
        /// <summary>
        /// Artificial Horizon, Extracted the original code from Whiplash's Artificial Horizon Redux
        /// </summary>
        class WhipsHorizon
        {
            readonly Program p;

            //readonly IMyShipController reference;
            public Color OrientationColor { get; set; } = new Color(150, 150, 150); // Lines in the sides color
            //Color progradeColor { get; set; } = new Color(150, 150, 0); //reticule old color
            public Color RetrogradeColor { get; set; } = new Color(150, 0, 0);  //Reverse Reticule Color
            public Color SkyColor { get; set; } = new Color(10, 30, 50); // Color of sky over the horizon line
            public Color HorizonLineColor { get; set; } = new Color(0, 0, 0); // Horizon line, defaults to transparent
            public Color ElevationLineColor { get; set; } = new Color(150, 150, 150); // Elevations line, can be more white, but I'll left it like it is
            public Color Offlinecolor { get; set; } = new Color(99, 99, 99); //Gray color of the text box of damp, cruise, etc
            public Color Onlinecolor { get; set; } = new Color(255, 40, 40); //Red color of the text box of damp, cruise, etc
            public Color ForwardArrowColor { get; set; } = new Color(0, 175, 0, 150); // Green Color of the arrow if it is pointing to the front
            public Color TextBoxBackground { get; set; } = Color.Black; // Background of textbox of damp, cruise, etc 
            public Color ProgressBarBackground { get; set; } = Color.Black; // Background of textbox of damp, cruise, etc 
            public Color GearBackground { get; set; } = Color.Black; // Background of textbox of damp, cruise, etc 
            public float ReticuleSens { get; set; } = 1; // Reticule sensitivity, the more the value, the more sens will have the velocity reticule
            public float DampreticuleSens { get; set; } = 0.5f; // How far it needs to be from the center to trigger arrow mode while in dampeners
            public List<IMyTextSurface> Surfaces { get; set; } // Surfaces, gets updated every time

            readonly Color AxisArrowBackColor = new Color(10, 10, 10);
            //enum AxisEnum : byte { None = 0, X = 1, Y = 2, Z = 4 }


            const int updatespersecond = 6; //???

            float loading_rotation = 0;
            float loading_rotation1 = 0;
            float loading_rotation2 = 1; //To make turns not that parallel

            //float accel = 0;
            float roll = 0;
            float pitch = 0;
            float rollcos = 0;
            float rollsin = 0;
            float speed = 0;
            float collisiontimeproportion = 0;


            const float STATUS_TEXT_SIZE = 1.3f;
            const float AXIS_LINE_WIDTH = 8f;
            const float AXIS_TEXT_OFFSET = 24f;
            const float AXIS_LENGTH_SCALE = 0.6f;
            const float HORIZON_THICKNESS = 5f;
            const float ELEVATION_TEXT_SIZE = 0.8f;
            const float ONE_OVER_HALF_PI = 1f / MathHelper.PiOver2;

            double vertspeed = 0;
            double surfalt = 0;
            double lastsurfalt = 0;
            double bearing = 0;
            double collisiontimethreshold = 5;

            //Vector3D gravity = Vector3D.Zero;
            //Vector3D velocity = Vector3D.Zero;
            //Vector3D lastvelocity = Vector3D.Zero;
            Vector3D _sunRotationAxis = new Vector3D(0, -1, 0);
            Vector3D _axisZCosVector;

            Vector2 flatennedvelocity = Vector2.Zero;
            Vector2 _xAxisFlattened;
            Vector2 _xAxisSign;
            Vector2 _xAxisDirn;
            Vector2 rollOffset;
            Vector2 pitchOffset;

            readonly Vector2 VELOCITY_INDICATOR_SIZE = new Vector2(64, 64);
            readonly Vector2 ELEVATION_LADDER_SIZE = new Vector2(175, 32);
            readonly Vector2 AXIS_MARKER_SIZE = new Vector2(24, 48);
            readonly Vector2 RETROGRADE_CROSS_SIZE = new Vector2(32, 4);

            bool ingravity = false;
            bool movingbackwards = false;
            bool outofscreen = false;
            bool CollisionWarning = false;
            bool lastCollisionWarning = false;
            bool showpullup = false;

            readonly string[] _axisIcon = new string[3];
            readonly CircularBuffer<double> velbuffer = new CircularBuffer<double>(5);
            public WhipsHorizon(List<IMyTextSurface> sfs, Program p/*, IMyShipController c*/)
            {
                Surfaces = sfs;
                this.p = p;
                //reference = c;
            }

            public void Process()
            {
                Calculate();
                Draw();
            }

            void Calculate()
            {
                //gravity = reference.GetNaturalGravity();
                //velocity = reference.GetShipVelocities().LinearVelocity;
                //accel = (float)((velocity - lastvelocity) * updatespersecond).Length();
                //lastvelocity = velocity;
                //Vector3D mov = Vector3D.TransformNormal(reference.MoveIndicator, reference.WorldMatrix);

                if ((p.mvin != 0 && !Vector3D.IsZero(p.shipVelocity, 1e-2) || p.almostbraked))
                {
                    Vector3D velocityNorm = p.shipVelocity;
                    speed = (float)velocityNorm.Normalize();
                    Vector3D localVelocity = Vector3D.Rotate(velocityNorm, MatrixD.Transpose(p.mainController.TheBlock.WorldMatrix));
                    flatennedvelocity.X = (float)Math.Asin(MathHelper.Clamp(localVelocity.X, -1, 1)) * ONE_OVER_HALF_PI;
                    flatennedvelocity.Y = (float)Math.Asin(MathHelper.Clamp(-localVelocity.Y, -1, 1)) * ONE_OVER_HALF_PI;
                    movingbackwards = localVelocity.Z > 1e-3;
                }
                else
                {
                    speed = 0;
                    flatennedvelocity = Vector2.Zero;
                    movingbackwards = false;
                }

                ingravity = !Vector3D.IsZero(p.worldGrav);
                if (ingravity)
                {
                    CalculateArtificialHorizonParameters(p.mainController.TheBlock, updatespersecond);
                }
                //if (outofscreen) CalculateArrowParameters(reference); moved due to incorrect arrow direction
            }

            void Draw()
            {
                foreach (IMyTextSurface s in Surfaces)
                {
                    RectangleF _viewport_s = new RectangleF(
                        (s.TextureSize - s.SurfaceSize) / 2f,
                        s.SurfaceSize
                     );

                    using (var frame = s.DrawFrame())
                    {
                        p.SetupDrawSurface(s);

                        Vector2 surfaceSize = s.TextureSize;
                        Vector2 screenCenter = surfaceSize * 0.5f;
                        Vector2 avgViewportSize = s.SurfaceSize - 12f;
                        float minSideLength = Math.Min(avgViewportSize.X, avgViewportSize.Y);
                        Vector2 squareViewportSize = new Vector2(minSideLength, minSideLength);
                        Vector2 scaleVec = (surfaceSize + avgViewportSize) * 0.5f / 512f;
                        float minScale = Math.Min(scaleVec.X, scaleVec.Y);

                        float progressbarsize = 1.45f;
                        float positionpbar = _viewport_s.Y + _viewport_s.Height - progressbarsize * minScale * 20;
                        float positionbar2 = positionpbar - progressbarsize * minScale * 25;

                        if (!p.parked && !p.trulyparked)
                        {

                            float sign = movingbackwards ? -1 : 1;

                            Vector2 velwh = squareViewportSize * flatennedvelocity * sign * ReticuleSens;
                            double farfromcenter = velwh.Length();
                            Vector2 velpos = screenCenter + velwh;
                            float minscreenCenter = Math.Min(screenCenter.X, screenCenter.Y);
                            float mindampSens = minSideLength / 2 * DampreticuleSens;

                            outofscreen = farfromcenter > minSideLength / 2 || !p.mainController.TheBlock.DampenersOverride && farfromcenter > mindampSens ||
                                ((velpos - screenCenter).Y > 0 && velpos.Y > positionbar2);

                            if (ingravity) DrawArtificialHorizon(frame, screenCenter, minScale, minSideLength);
                            if (outofscreen)
                            {
                                CalculateArrowParameters(p.mainController.TheBlock);
                                DrawArrow(frame, screenCenter, minSideLength * 0.5f, minScale);
                            }

                            DrawLine(frame, new Vector2(0, screenCenter.Y), new Vector2(screenCenter.X - 64 * minScale, screenCenter.Y), HORIZON_THICKNESS * minScale, OrientationColor);
                            DrawLine(frame, new Vector2(screenCenter.X + 64 * minScale, screenCenter.Y), new Vector2(screenCenter.X * 2f, screenCenter.Y), HORIZON_THICKNESS * minScale, OrientationColor);

                            Vector2 scaledIconSize = VELOCITY_INDICATOR_SIZE * minScale;
                            MySprite centerSprite = new MySprite(SpriteType.TEXTURE, "AH_BoreSight", size: scaledIconSize * 1.2f, position: screenCenter + Vector2.UnitY * scaledIconSize * 0.5f, color: OrientationColor)
                            {
                                RotationOrScale = -MathHelper.PiOver2
                            };
                            frame.Add(centerSprite);

                            if (!outofscreen)
                            {
                                // Draw velocity indicator
                                MySprite velocitySprite = new MySprite(SpriteType.TEXTURE, "AH_VelocityVector", size: scaledIconSize, color: !movingbackwards ? ForwardArrowColor/*progradeColor*/ : RetrogradeColor)
                                {
                                    Position = velpos
                                };
                                frame.Add(velocitySprite);

                                if (movingbackwards)
                                {
                                    Vector2 retrogradeCrossSize = RETROGRADE_CROSS_SIZE * minScale;
                                    MySprite retrograteSprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: retrogradeCrossSize, color: RetrogradeColor)
                                    {
                                        Position = velocitySprite.Position,
                                        RotationOrScale = MathHelper.PiOver4
                                    };
                                    frame.Add(retrograteSprite);
                                    retrograteSprite.RotationOrScale += MathHelper.PiOver2;
                                    frame.Add(retrograteSprite);
                                }
                            }
                        }

                        Vector2 position_s;

                        float lenghtpb = 200 / 2 * minScale * progressbarsize + 140 / 2 * minScale * 1.5f; //used to calculate lenght of the progressbar
                        float lengthtb = 74 * minScale * progressbarsize; //used to calculate lenght of the 1st square accels

                        position_s = new Vector2(_viewport_s.X + (_viewport_s.Width * 0.5f) - lengthtb, positionpbar);

                        float percent = (float)(p.gearaccel / p.maxaccel * 100);

                        DrawProgressBar(frame, position_s, percent, minScale * progressbarsize, center: true, background: ProgressBarBackground);

                        position_s = new Vector2(position_s.X + lenghtpb, positionpbar);

                        TextBox(frame, position_s, $"{Math.Round(p.accel_aux, 2)} m/s²", minScale * progressbarsize, background: TextBoxBackground);

                        position_s = new Vector2(_viewport_s.X + (_viewport_s.Width * 0.5f), positionbar2);

                        DrawGear(frame, position_s, minScale * progressbarsize, background: GearBackground);

                        float[] wh1 = new float[] { _viewport_s.Width * 0.15f, _viewport_s.Height * 0.25f };
                        float[] wh2 = new float[] { _viewport_s.Width * 0.85f, _viewport_s.Height * 0.25f };
                        float[] wh3 = new float[] { _viewport_s.Width * 0.15f, _viewport_s.Height * 0.4f };
                        float divisor = 1;

                        if (p.mainController.TheBlock.BlockDefinition.ToString().Contains("FighterCockpit") &&
                            (p.mainController.TheBlock as IMyTextSurfaceProvider).GetSurface(0).Equals(s))
                        {
                            wh1 = new float[] { _viewport_s.Width * 0.225f, _viewport_s.Height * 0.4f };
                            wh2 = new float[] { _viewport_s.Width * 0.75f, _viewport_s.Height * 0.4f };
                            wh3 = new float[] { _viewport_s.Width * 0.225f, _viewport_s.Height * 0.60f };
                            divisor = 1.1f;
                        }
                        position_s = new Vector2(_viewport_s.X + wh1[0], _viewport_s.Y + wh1[1]);

                        TextBox(frame, position_s, $"CRUISE", minScale * progressbarsize / divisor, 80, p.cruise ? Onlinecolor : Offlinecolor, TextBoxBackground);

                        position_s = new Vector2(_viewport_s.X + wh2[0], _viewport_s.Y + wh2[1]);

                        TextBox(frame, position_s, $"PARK", minScale * progressbarsize / divisor, 80, p.allowpark ? Onlinecolor : Offlinecolor, TextBoxBackground);

                        position_s = new Vector2(_viewport_s.X + wh3[0], _viewport_s.Y + wh3[1]);

                        TextBox(frame, position_s, $"DAMP", minScale * progressbarsize / divisor, 80, p.dampeners ? Onlinecolor : Offlinecolor, TextBoxBackground);

                        Vector2 LoadingPos = new Vector2(screenCenter.X, screenCenter.Y - minScale * 50);
                        Vector2 LTextPos = new Vector2(screenCenter.X, screenCenter.Y + 25f * minScale + 3f);
                        Vector2 PrinterPos = new Vector2(screenCenter.X, _viewport_s.Y + 12f * minScale);
                        Vector2 BTextBox = new Vector2(_viewport_s.X + (_viewport_s.Width * 0.5f), _viewport_s.Y + (_viewport_s.Height * 0.5f) - minScale * 50);

                        if (p.parkedcompletely && p.BlockManager.Doneloop)
                        {
                            Write("PARKED", frame, BTextBox, minScale * 3.5f);
                        } //PARKED
                        else if (p.parkedcompletely && !p.BlockManager.Doneloop)
                        {
                            Write("ASSIGNING", frame, LTextPos, minScale * 2);
                            Loading(frame, LoadingPos, minScale * 2, -0.5f);
                        } //ASSIGNING
                        else if (!p.parked && p.alreadyparked && !p.BlockManager.Doneloop)
                        {
                            Write("UNPARKING", frame, LTextPos, minScale * 2);
                            Loading(frame, LoadingPos, minScale * 2, 1.2f);
                        } //UNPARKING

                        if (((p.parked && p.alreadyparked) || p.trulyparked) && p.setTOV && (p.totalVTThrprecision.Round(1) != 100 || p.temp1 <= 0.5))
                        {
                            Write("PARKING", frame, LTextPos, minScale * 2);
                            Loading(frame, LoadingPos, minScale * 2, 0.5f);
                        } //PARKING
                        else if (p.trulyparked && !p.parked)
                        {
                            Write("(NOT) PARKED", frame, BTextBox, minScale * 3f);
                        }


                        Write(p.screensb.ToString(), frame, PrinterPos, minScale);

                        frame.Dispose();
                    }
                }


            }

            public void BSOD()
            {

                foreach (IMyTextSurface s in Surfaces)
                {
                    p.SetupDrawSurface(s, new Color(0, 0, 65, 255));
                    //-----
                    Vector2 surfaceSize = s.TextureSize;
                    Vector2 screenCenter = surfaceSize * 0.5f;
                    Vector2 avgViewportSize = s.SurfaceSize - 12f;
                    float minSideLength = Math.Min(avgViewportSize.X, avgViewportSize.Y);
                    Vector2 squareViewportSize = new Vector2(minSideLength, minSideLength);
                    Vector2 scaleVec = (surfaceSize + avgViewportSize) * 0.5f / 512f;
                    float minScale = Math.Min(scaleVec.X, scaleVec.Y);

                    using (var frame = s.DrawFrame())
                    {
                        DrawBSOD(frame, screenCenter, avgViewportSize, minScale);
                    }
                    //------
                }
            }

            public Vector2 NextL(Vector2 pos, float scale, float add = 0)
            {
                return new Vector2(pos.X, pos.Y + (16.5f + add) * scale);
            }

            public void DrawBSOD(MySpriteDrawFrame frame, Vector2 centerPos, Vector2 viewport, float scale = 1f)
            {
                Vector2 pos = new Vector2(centerPos.X - viewport.X * 0.5f, centerPos.Y - viewport.Y * 0.5f);
                float additional = 10f;
                float size1 = 0.54f;


                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "A problem has been detected and Vector Thrust OS has been shut down to",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line1

                pos = NextL(pos, scale);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "prevent damage to your Grid.",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line2

                pos = NextL(pos, scale, additional);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "The problem seems to be caused by the following reason: REASON.SYS",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line3

                pos = NextL(pos, scale, additional);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "REASON_REASON_REASON_REASON",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line4

                pos = NextL(pos, scale, additional);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "If this is the first time you've seen this Stop error screen, recompile your",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line5

                pos = NextL(pos, scale);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "Programmable Block. If this screen appears again, follow these steps:",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line6

                pos = NextL(pos, scale, additional);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "Check to make sure any new blocks and PB are properly installed.",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line7

                pos = NextL(pos, scale);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "If this is a new installation, ask your grid or script manufacturer for any",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line8

                pos = NextL(pos, scale);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "Vector Thrust OS updates you might need.",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line9

                pos = NextL(pos, scale, additional);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "If problems continue, disable or remove any newly installed blocks or scripts. ",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line10

                pos = NextL(pos, scale);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "Disable CUSTOM_DATA options such as nametag or surrounds. If you need to",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line11

                pos = NextL(pos, scale);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "Log to remove or disable something, go to the Custom Data of PB, Change Show",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line12

                pos = NextL(pos, scale);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "Metrics, and then Recompile the Script.",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line13

                pos = NextL(pos, scale, additional);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "Technical information:",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line14

                pos = NextL(pos, scale, additional);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "*** STOP: 0x0000050 (0xFD3094C2, 0x00000001, 0xFBFE7617, 0x00000000)",
                    Position = pos,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line15

                pos = NextL(pos, scale, additional + 10);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "*** REASON.SYS -- Address VTOS0822 base at VTOS2022, DateStamp vant666p2",
                    Position = pos, // new Vector2(-250f, 189f) * scale + centerPos
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = size1 * scale
                }); // Line16
            }

            public void Write(string text, MySpriteDrawFrame frame, /*Vector2 centerPos, */Vector2 position, float scale = 1f, bool center = true)
            {
                TextAlignment al = center ? TextAlignment.CENTER : TextAlignment.LEFT;
                MySprite sp = new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = al,
                    Data = text,
                    Position = position,
                    Color = new Color(255, 255, 255, 255),
                    FontId = "Debug",
                    RotationOrScale = 1f * scale
                };

                // Echo("Size:" + sp.Size.ToString());

                frame.Add(sp); // text3
            }


            MySprite DrawLine(Vector2 point1, Vector2 point2, float width, Color color)
            {
                Vector2 position = 0.5f * (point1 + point2);
                Vector2 diff = point1 - point2;
                float length = diff.Length();
                if (length > 0)
                    diff /= length;

                Vector2 size = new Vector2(length, width);
                float angle = (float)Math.Acos(Vector2.Dot(diff, Vector2.UnitX));
                angle *= Math.Sign(Vector2.Dot(diff, Vector2.UnitY));

                MySprite sprite = MySprite.CreateSprite("SquareSimple", position, size);
                sprite.RotationOrScale = angle;
                sprite.Color = color;
                return sprite;
            }

            void DrawLine(MySpriteDrawFrame frame, Vector2 point1, Vector2 point2, float width, Color color)
            {
                Vector2 position = 0.5f * (point1 + point2);
                Vector2 diff = point1 - point2;
                float length = diff.Length();
                if (length > 0)
                    diff /= length;

                Vector2 size = new Vector2(length, width);
                float angle = (float)Math.Acos(Vector2.Dot(diff, Vector2.UnitX));
                angle *= Math.Sign(Vector2.Dot(diff, Vector2.UnitY));

                MySprite sprite = MySprite.CreateSprite("SquareSimple", position, size);
                sprite.RotationOrScale = angle;
                sprite.Color = color;
                frame.Add(sprite);
            }

            void DrawArrowHead(MySpriteDrawFrame frame, Vector2 position, Vector2 arrowSize, Vector2 flattenedDirection, double depthSin, Color color, Color backColor)
            {
                if (Math.Abs(flattenedDirection.LengthSquared() - 1) < MathHelper.EPSILON)
                    flattenedDirection.Normalize();

                arrowSize.Y *= (float)Math.Sqrt(1 - depthSin * depthSin);
                Vector2 baseSize = Vector2.One * arrowSize.X;
                baseSize.Y *= (float)Math.Abs(depthSin);

                float angle = (float)Math.Acos(Vector2.Dot(flattenedDirection, -Vector2.UnitY));
                angle *= Math.Sign(Vector2.Dot(flattenedDirection, Vector2.UnitX));

                Vector2 trianglePosition = position + flattenedDirection * arrowSize.Y * 0.5f;

                MySprite circle = MySprite.CreateSprite("Circle", position, baseSize);

                circle.Color = movingbackwards ? color : backColor;
                circle.RotationOrScale = angle;

                MySprite triangle = MySprite.CreateSprite("Triangle", trianglePosition, arrowSize);
                triangle.Color = color;
                triangle.RotationOrScale = angle;

                frame.Add(triangle);
                frame.Add(circle);
            }

            public void TextBox(MySpriteDrawFrame frame, Vector2 centerPos, string text, float scale = 1f, float width = 140f, Color? color = null, Color? background = null)
            {
                color = color ?? Color.White;

                if (background != null)
                {
                    frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Alignment = TextAlignment.CENTER,
                        Data = "SquareSimple",
                        Position = centerPos,
                        Size = new Vector2(width, 25f) * scale,
                        Color = background,
                        RotationOrScale = 0f
                    }); // sprite1
                }

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.CENTER,
                    Data = "SquareHollow",
                    Position = centerPos,
                    Size = new Vector2(width, 25f) * scale,
                    Color = color,
                    RotationOrScale = 0f
                }); // sprite1
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.CENTER,
                    Data = text,
                    Position = new Vector2(centerPos.X, centerPos.Y - scale * 12.5f),
                    Color = color,
                    FontId = "Debug",
                    RotationOrScale = 0.75f * scale
                }); // text2
            }

            void DrawProgressBar(MySpriteDrawFrame frame, Vector2 centerPos, float percentage, float scale = 1f, bool center = true, Color? color = null, Color ? barcolor = null, Color ? background = null)
            {
                Vector2 pos = center ? new Vector2(centerPos.X - (200f * scale / 2.25f), centerPos.Y) : centerPos;
                TextAlignment ta = center ? TextAlignment.LEFT : TextAlignment.CENTER;

                percentage = MathHelper.Clamp(percentage, 0, 100);

                percentage = (percentage * 180) / 100;

                color = color ?? Color.White;
                barcolor = barcolor ?? new Color(0, 255, 255, 255);

                if (background != null)
                {
                    frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Alignment = TextAlignment.CENTER,
                        Data = "SquareSimple",
                        Position = centerPos,
                        Size = new Vector2(200f, 25f) * scale,
                        Color = background,
                        RotationOrScale = 0f
                    }); // sprite1
                }

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = ta,
                    Data = "SquareSimple",
                    Position = pos,
                    Size = new Vector2(percentage, 20f) * scale,
                    Color = barcolor,
                    RotationOrScale = 0f
                }); // sprite1Copy
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.CENTER,
                    Data = "SquareHollow",
                    Position = centerPos,
                    Size = new Vector2(200f, 25f) * scale,
                    Color = color,
                    RotationOrScale = 0f
                }); // sprite1
            }

            void DrawGear(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, Color? color = null, Color? background = null, Color? barcolor = null)
            {
                float width = 200f;
                float xcoord = -100 + 75 * 0.5f;
                color = color ?? Color.White;
                background = background ?? Color.Black;
                barcolor = barcolor ?? new Color(0, 255, 0, 255);

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.LEFT,
                    Data = "SquareSimple",
                    Position = new Vector2(xcoord, 0f) * scale + centerPos,
                    Size = new Vector2(width + scale * 12, 25f) * scale,
                    Color = Color.Black,
                    RotationOrScale = 0f
                });

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.LEFT,
                    Data = "SquareHollow",
                    Position = new Vector2(xcoord, 0f) * scale + centerPos,
                    Size = new Vector2(width + scale * 12, 25f) * scale,
                    Color = color,
                    RotationOrScale = 0f
                });

                float stocksize = 58f;
                float stockdiv = 61f;
                int stocknumber = 3;

                int dnum = p.Accelerations.Length;
                int cnum = p.gear + 1;

                int dif = dnum - cnum;

                float formula = stocksize * stocknumber / dnum;
                float formula2 = stockdiv * stocknumber / dnum;

                float currentpos = xcoord + width - formula2;

                for (int i = 0; i < dnum; i++)
                {
                    frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Alignment = TextAlignment.LEFT,
                        Data = "SquareSimple",
                        Position = new Vector2(currentpos, 0f) * scale + centerPos,
                        Size = new Vector2(formula, 20f) * scale,
                        Color = dif != 0 ? Color.Black : barcolor,
                        RotationOrScale = 0f
                    });


                    frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Alignment = TextAlignment.LEFT,
                        Data = "SquareHollow",
                        Position = new Vector2(currentpos, 0f) * scale + centerPos,
                        Size = new Vector2(formula, 25) * scale,
                        Color = color,
                        RotationOrScale = 0f
                    });

                    if (dif != 0) dif--;
                    currentpos -= formula2;
                }

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.LEFT,
                    Data = "SquareSimple",
                    Position = new Vector2(-100 - 75 * 0.5f, 0f) * scale + centerPos,
                    Size = new Vector2(75f, 25f) * scale,
                    Color = background,
                    RotationOrScale = 0f
                });

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.LEFT,
                    Data = "SquareHollow",
                    Position = new Vector2(-100 - 75 * 0.5f, 0f) * scale + centerPos,
                    Size = new Vector2(75f, 25f) * scale,
                    Color = color,
                    RotationOrScale = 0f
                });
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = "GEAR",
                    Position = new Vector2(-125, -13f) * scale + centerPos,
                    Color = color,
                    FontId = "Debug",
                    RotationOrScale = 0.8f * scale
                });
            }

            public void Loading(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float speed = 1)
            {
                Vector2 size = new Vector2(25f, 25f) * scale;

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.CENTER,
                    Data = "Screen_LoadingBar",
                    Position = centerPos,
                    Size = size,
                    Color = new Color(255, 255, 255, 255),
                    RotationOrScale = loading_rotation
                }); // sprite1
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.CENTER,
                    Data = "Screen_LoadingBar",
                    Position = centerPos,
                    Size = size * 2,
                    Color = new Color(255, 255, 255, 255),
                    RotationOrScale = loading_rotation1
                }); // sprite2Copy

                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.CENTER,
                    Data = "Screen_LoadingBar",
                    Position = centerPos,
                    Size = size * 3.5f,
                    Color = new Color(255, 255, 255, 255),
                    RotationOrScale = loading_rotation2
                }); // sprite2Copy

                //360º
                speed *= 0.0125f;

                loading_rotation += speed * 3 - MathHelper.TwoPi;
                loading_rotation1 -= speed * 3 + MathHelper.TwoPi;
                loading_rotation2 += speed * 3 - MathHelper.TwoPi;

                if (loading_rotation + 2100 < 10) loading_rotation = loading_rotation1 = loading_rotation2 = 0;
            }

            void CalculateArtificialHorizonParameters(IMyShipController controller, double updatesPerSecond)
            {
                Vector3D up = -p.worldGrav;
                Vector3D left = Vector3D.Cross(up, controller.WorldMatrix.Forward);
                Vector3D forward = Vector3D.Cross(left, up);

                var localUpVector = Vector3D.Rotate(up, MatrixD.Transpose(controller.WorldMatrix));
                var flattenedUpVector = new Vector3D(localUpVector.X, localUpVector.Y, 0);
                roll = (float)p.AngleBetween(flattenedUpVector, Vector3D.Up) * Math.Sign(Vector3D.Dot(Vector3D.Right, flattenedUpVector));
                pitch = (float)p.AngleBetween(forward, controller.WorldMatrix.Forward) * Math.Sign(Vector3D.Dot(up, controller.WorldMatrix.Forward));

                rollcos = MyMath.FastCos(roll);
                rollsin = MyMath.FastSin(roll);

                double alt;
                controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);
                surfalt = alt;

                controller.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out alt);
                surfalt = alt;

                velbuffer.Add((lastsurfalt - surfalt) * updatesPerSecond);
                double velocitySum = 0;
                for (int i = 0; i < velbuffer.Capacity; ++i)
                {
                    velocitySum += velbuffer.MoveNext();
                }

                double terrainHeightDerivative = velocitySum / velbuffer.Capacity;
                double timeTillGroundCollision = surfalt / (terrainHeightDerivative);
                collisiontimeproportion = (float)(timeTillGroundCollision / collisiontimethreshold);
                CollisionWarning = terrainHeightDerivative > 0 && speed > 10 && timeTillGroundCollision <= collisiontimethreshold;
                if (lastCollisionWarning != CollisionWarning)
                    showpullup = true;
                else
                    showpullup = !showpullup;

                lastCollisionWarning = CollisionWarning;
                lastsurfalt = surfalt;

                Vector3D eastVec = Vector3D.Cross(p.worldGrav, _sunRotationAxis);
                Vector3D northVec = Vector3D.Cross(eastVec, p.worldGrav);
                Vector3D heading = p.Rejection(controller.WorldMatrix.Forward, p.worldGrav);

                bearing = MathHelper.ToDegrees(p.AngleBetween(heading, northVec));
                if (Vector3D.Dot(controller.WorldMatrix.Forward, eastVec) < 0)
                    bearing = 360 - bearing;

                vertspeed = ScalarProjection(p.shipVelocity, -p.worldGrav);
            }

            void DrawElevationLadder(MySpriteDrawFrame frame, Vector2 midPoint, Vector2 size, float basePitchProportion, float elevationAngleDeg, float scale/*, Color color*/, bool drawText)
            {
                float pitchProportion = MathHelper.ToRadians(-elevationAngleDeg) / MathHelper.PiOver2;
                string textureName = pitchProportion <= 0 ? "AH_GravityHudPositiveDegrees" : "AH_GravityHudNegativeDegrees";
                Vector2 scaledSize = size * scale;

                MySprite ladderSprite = new MySprite(SpriteType.TEXTURE, textureName, color: ElevationLineColor, size: scaledSize)
                {
                    RotationOrScale = roll + (pitchProportion <= 0 ? MathHelper.Pi : 0),
                    Position = midPoint + (pitchProportion + basePitchProportion) * pitchOffset
                };
                frame.Add(ladderSprite);

                if (!drawText)
                    return;

                Vector2 textHorizontalOffset = new Vector2(rollcos, rollsin) * (scaledSize.X + 48f * scale) * 0.5f;
                Vector2 textVerticalOffset = Vector2.UnitY * -24f * scale * (pitchProportion <= 0 ? 0 : 1);

                MySprite text = MySprite.CreateText($"{elevationAngleDeg}", "Debug", ElevationLineColor);
                text.RotationOrScale = ELEVATION_TEXT_SIZE * scale;
                text.Position = ladderSprite.Position + textHorizontalOffset + textVerticalOffset;
                frame.Add(text);

                text.Position = ladderSprite.Position - textHorizontalOffset + textVerticalOffset;
                frame.Add(text);
            }

            void DrawArtificialHorizon(MySpriteDrawFrame frame, Vector2 screenCenter, float scale, float minSideLength)
            {
                Vector2 skySpriteSize = screenCenter * 6f;
                rollOffset.Y = skySpriteSize.Y * 0.5f * (1 - rollcos);
                rollOffset.X = skySpriteSize.Y * 0.5f * (rollsin);
                pitchOffset.Y = rollcos * minSideLength * 0.5f;
                pitchOffset.X = -rollsin * minSideLength * 0.5f;
                float pitchProportion = pitch / MathHelper.PiOver2;

                MySprite skySprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: SkyColor, size: skySpriteSize)
                {
                    RotationOrScale = roll
                };

                Vector2 skyMidPt = screenCenter + new Vector2(0, -skySpriteSize.Y * 0.5f); //surfaceSize.Y * new Vector2(0.5f, -1f);
                skySprite.Position = skyMidPt + rollOffset + pitchOffset * pitchProportion;
                frame.Add(skySprite);

                // Draw horizon line
                MySprite horizonLineSprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: HorizonLineColor, size: new Vector2(skySpriteSize.X, HORIZON_THICKNESS * scale))
                {
                    RotationOrScale = roll,
                    Position = screenCenter + pitchOffset * pitchProportion,
                };
                frame.Add(horizonLineSprite);

                for (int i = -90; i <= 90; i += 30)
                {
                    if (i == 0)
                        continue;
                    DrawElevationLadder(frame, screenCenter, ELEVATION_LADDER_SIZE, pitchProportion, i, scale, true);
                }
            }

            void CalculateArrowParameters(IMyShipController controller)
            {
                // Flattening axes onto the screen surface
                MatrixD transposedMatrix = MatrixD.Transpose(controller.WorldMatrix);
                Vector3D xTrans = Vector3D.Rotate(p.shipVelocity / p.sv, transposedMatrix);

                _xAxisFlattened.X = (float)(xTrans.X) * AXIS_LENGTH_SCALE;
                _xAxisFlattened.Y = (float)(-xTrans.Y) * AXIS_LENGTH_SCALE;

                _xAxisSign = Vector2.SignNonZero(_xAxisFlattened);

                if (!Vector2.IsZero(ref _xAxisFlattened, MathHelper.EPSILON))
                    _xAxisDirn = Vector2.Normalize(_xAxisFlattened);

                _axisIcon[0] = GetAxisIcon(xTrans.Z);
                double max = _axisZCosVector.Max();
                double min = _axisZCosVector.Min();
            }

            void DrawArrow(MySpriteDrawFrame frame, Vector2 screenCenter, float halfExtent, float scale)
            {
                float textSize = scale * STATUS_TEXT_SIZE;
                float lineSize = scale * AXIS_LINE_WIDTH;
                float offset = scale * AXIS_TEXT_OFFSET;
                Vector2 markerSize = scale * AXIS_MARKER_SIZE;
                Vector2 xPos = screenCenter + _xAxisFlattened * halfExtent;
                MySprite xLine = DrawLine(screenCenter, xPos, lineSize, !movingbackwards ? ForwardArrowColor : RetrogradeColor);

                MySprite xLabel = MySprite.CreateText("DIR", "Debug", !movingbackwards ? ForwardArrowColor : RetrogradeColor, textSize, TextAlignment.CENTER);
                xLabel.Position = xPos + offset * _xAxisSign - Vector2.UnitY * markerSize.Y;

                DrawArrowHead(frame, xPos, AXIS_MARKER_SIZE * scale, _xAxisDirn, _axisZCosVector.X, !movingbackwards ? ForwardArrowColor : RetrogradeColor, AxisArrowBackColor);
                frame.Add(xLine);
                frame.Add(xLabel);
            }

            string GetAxisIcon(double z)
            {
                return z < 0 ? "CircleHollow" : "Circle";
            }
        }
        #endregion

        public void SetupDrawSurface(IMyTextSurface surface, Color? color = null)
        {
            color = color ?? new Color(0, 0, 0, 255);
            // Draw background color
            surface.ScriptBackgroundColor = (Color)color;

            // Set content type
            surface.ContentType = ContentType.SCRIPT;

            // Set script to none
            surface.Script = "";
        }

        #region Circular Buffer
        /// <summary>
        /// A simple, generic circular buffer class with a fixed capacity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class CircularBuffer<T>
        {
            public readonly int Capacity;

            readonly T[] _array = null;
            int _setIndex = 0;
            int _getIndex = 0;

            /// <summary>
            /// CircularBuffer ctor.
            /// </summary>
            /// <param name="capacity">Capacity of the CircularBuffer.</param>
            public CircularBuffer(int capacity)
            {
                if (capacity < 1)
                    throw new Exception($"Capacity of CircularBuffer ({capacity}) can not be less than 1");
                Capacity = capacity;
                _array = new T[Capacity];
            }

            /// <summary>
            /// Adds an item to the buffer. If the buffer is full, it will overwrite the oldest value.
            /// </summary>
            /// <param name="item"></param>
            public void Add(T item)
            {
                _array[_setIndex] = item;
                _setIndex = ++_setIndex % Capacity;
            }

            /// <summary>
            /// Retrieves the current item in the buffer and increments the buffer index.
            /// </summary>
            /// <returns></returns>
            public T MoveNext()
            {
                T val = _array[_getIndex];
                _getIndex = ++_getIndex % Capacity;
                return val;
            }

            /// <summary>
            /// Retrieves the current item in the buffer without incrementing the buffer index.
            /// </summary>
            /// <returns></returns>
            public T Peek()
            {
                return _array[_getIndex];
            }
        }
        #endregion

        public static double ScalarProjection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;

            if (Vector3D.IsUnit(ref b))
                return a.Dot(b);

            return a.Dot(b) / b.Length();
        }

        public Vector3D Rejection(Vector3D a, Vector3D b) //reject a on b
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return Vector3D.Zero;

            return a - a.Dot(b) / b.LengthSquared() * b;
        }

        public double AngleBetween(Vector3D a, Vector3D b) //returns radians
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }
    }
}
