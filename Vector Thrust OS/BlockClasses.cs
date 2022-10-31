using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {

        class VectorThrust
        {
            readonly Program p;

            // physical parts
            public Rotor rotor;
            public List<Thruster> thrusters;// all the thrusters
            public List<Thruster> availableThrusters;// <= thrusters: the ones the user chooses to be used (ShowInTerminal)
            public List<Thruster> activeThrusters;// <= activeThrusters: the ones that are facing the direction that produces the most thrust (only recalculated if available thrusters changes)

            public Vector3D requiredVec = Vector3D.Zero;

            public float totalEffectiveThrust = 0;

            public int detectThrustCounter = 0;
            public Vector3D currDir = Vector3D.Zero;

            public VectorThrust(Rotor rotor, Program program)
            {
                this.p = program;
                this.rotor = rotor;
                this.thrusters = new List<Thruster>();
                this.availableThrusters = new List<Thruster>();
                this.activeThrusters = new List<Thruster>();
            }

            // final calculations and setting physical components
            public void Go()
            {
                
                double angleCos = rotor.Point(requiredVec);

                //0.064-0.077 0.072-0.080-0.126 

                // the clipping value 'thrustModifier' defines how far the rotor can be away from the desired direction of thrust, and have the power still at max
                // if 'thrustModifier' is at 1, the thruster will be at full desired power when it is at 90 degrees from the direction of travel
                // if 'thrustModifier' is at 0, the thruster will only be at full desired power when it is exactly at the direction of travel, (it's never exactly in-line)

                double tmod = MathHelper.Clamp(p.thrustermodifier, 0, 1); //Temporal

                // put it in some graphing calculator software where 'angleCos' is cos(x) and adjust the thrustModifier values between 0 and 1, then you can visualise it
                double thrustOffset = ((((angleCos + 1) * (1 + tmod)) / 2) - tmod) * (((angleCos + 1) * (1 + tmod)) / 2);// the other one is simpler, but this one performs better
                thrustOffset = MathHelper.Clamp(thrustOffset, 0, 1);

                //set the thrust for each engine
                foreach (Thruster thruster in activeThrusters)
                {
                    Vector3D thrust = (thrustOffset * requiredVec * thruster.TheBlock.MaxEffectiveThrust / totalEffectiveThrust);
                    bool noThrust = thrust.LengthSquared() < 0.001f || (p.wgv == 0 && angleCos < 0.85);
                    p.tthrust += noThrust ? 0 : MathHelper.Clamp(thrust.Length(), 0, thruster.TheBlock.MaxEffectiveThrust);

                    if (!p.thrustOn || noThrust)
                    {
                        thruster.SetThrust(0);
                        thruster.TheBlock.Enabled = false;
                        thruster.IsOffBecauseDampeners = !p.thrustOn || noThrust;
                    }
                    else
                    {
                        thruster.SetThrust(thrust);
                        thruster.TheBlock.Enabled = true;
                        thruster.IsOffBecauseDampeners = false;
                    }
                }

                //0.09-0.13 0.950.132
            }

            // New thruster group assigning system
            public void AssignGroup()
            {
                bool foundGroup = false;

                IMyMotorStator crt = rotor.TheBlock;
                MatrixD wm1 = crt.WorldMatrix;
                Vector3D wmu1 = wm1.Up;

                foreach (List<VectorThrust> g in p.VTThrGroups)
                {
                    IMyMotorStator nrt = g[0].rotor.TheBlock;
                    MatrixD wm2 = nrt.WorldMatrix;
                    Vector3D wmu2 = wm2.Up;

                    if (Vector3D.Dot(wmu1, wmu2).Abs() > 0.9)
                    {
                        if (rotor.isHinge.Equals(g[0].rotor.isHinge)) {
                            if ((rotor.isHinge && Vector3D.Dot(wm1.Left, wm2.Left) > 0.9) || !rotor.isHinge) {
                                if (!g.Contains(this)) g.Add(this);
                                foundGroup = true;
                                //p.Echo($"Found Group");
                                break;
                            }
                        }
                    }
                }
                if (!foundGroup)
                {// if it never found a group, add a group
                    p.VTThrGroups.Add(new List<VectorThrust>());
                    p.VTThrGroups[p.VTThrGroups.Count - 1].Add(this);
                    p.tets.Add(0); //Add empty slot of TotalEffectiveThrust, just in case it crashes
                }
            }



            public double CalcTotalEffectiveThrust()
            {
                totalEffectiveThrust = 0;
                foreach (Thruster t in activeThrusters)
                {
                    totalEffectiveThrust += t.TheBlock.MaxEffectiveThrust;
                }
                return totalEffectiveThrust;
            }

            //true if all thrusters are good
            public bool ValidateThrusters()
            {
                bool needsUpdate = false;
                foreach (Thruster curr in thrusters)
                {

                    bool shownAndFunctional = curr.TheBlock.IsFunctional;
                    if (availableThrusters.Contains(curr))
                    {//is available

                        bool wasOnAndIsNowOff = curr.IsOn && !curr.TheBlock.Enabled && !curr.IsOffBecauseDampeners;

                        if ((!shownAndFunctional || wasOnAndIsNowOff))
                        {
                            curr.IsOn = false;
                            //remove the thruster
                            availableThrusters.Remove(curr);
                            needsUpdate = true;
                        }

                    }
                    else
                    {//not available
                        bool wasOffAndIsNowOn = !curr.IsOn && curr.TheBlock.Enabled;
                        if (shownAndFunctional && wasOffAndIsNowOn)
                        {
                            availableThrusters.Add(curr);
                            needsUpdate = true;
                            curr.IsOn = true;
                        }
                    }
                }
                return !needsUpdate;
            }

            public void DetectThrustDirection()
            {
                Vector3D engineDirection = Vector3D.Zero;
                Vector3D engineDirectionNeg = Vector3D.Zero;
                Vector3I thrustDir = Vector3I.Zero;
                Base6Directions.Direction rotTopUp = rotor.TheBlock.Top.Orientation.Up;

                bool allthrustersoff = thrusters.All(x => !x.TheBlock.Enabled);

                // add all the thrusters effective power
                foreach (Thruster t in availableThrusters)
                {
                    Base6Directions.Direction thrustForward = t.TheBlock.Orientation.Forward; // Exhaust goes this way

                    //if its not facing rotor up or rotor down
                    if (!(thrustForward == rotTopUp || thrustForward == Base6Directions.GetFlippedDirection(rotTopUp)))
                    {
                        // add it in
                        var thrustForwardVec = Base6Directions.GetVector(thrustForward);
                        if (thrustForwardVec.X < 0 || thrustForwardVec.Y < 0 || thrustForwardVec.Z < 0)
                        {
                            engineDirectionNeg += Base6Directions.GetVector(thrustForward) * t.TheBlock.MaxEffectiveThrust;
                        }
                        else
                        {
                            engineDirection += Base6Directions.GetVector(thrustForward) * t.TheBlock.MaxEffectiveThrust;
                        }
                    }
                }

                // get single most powerful direction
                double max = Math.Max(engineDirection.Z, Math.Max(engineDirection.X, engineDirection.Y));
                double min = Math.Min(engineDirectionNeg.Z, Math.Min(engineDirectionNeg.X, engineDirectionNeg.Y));
                double maxAbs;
                if (max > -1 * min)
                {
                    maxAbs = max;
                }
                else
                {
                    maxAbs = min;
                }

                // TODO: swap onbool for each thruster that isn't in this
                float DELTA = 0.1f;
                if (Math.Abs(maxAbs - engineDirection.X) < DELTA)
                {
                    thrustDir.X = 1;
                }
                else if (Math.Abs(maxAbs - engineDirection.Y) < DELTA)
                {
                    thrustDir.Y = 1;
                }
                else if (Math.Abs(maxAbs - engineDirection.Z) < DELTA)
                {
                    thrustDir.Z = 1;
                }
                else if (Math.Abs(maxAbs - engineDirectionNeg.X) < DELTA)
                {
                    thrustDir.X = -1;
                }
                else if (Math.Abs(maxAbs - engineDirectionNeg.Y) < DELTA)
                {
                    thrustDir.Y = -1;
                }
                else if (Math.Abs(maxAbs - engineDirectionNeg.Z) < DELTA)
                {
                    thrustDir.Z = -1;
                }
                else
                {
                   return;
                }

                // use thrustDir to set rotor offset (IF THRUSTERS ARE MANUALLY OFF, WON'T DO ANYTHING)
                if (!allthrustersoff) rotor.direction = (Vector3D)thrustDir;

                // put thrusters into the active list
                Base6Directions.Direction thrDir = Base6Directions.GetDirection(thrustDir);
                ActiveList(thrDir);
            }

            public void ActiveList(Base6Directions.Direction? thrDir = null, bool Override = false) {
                if (!Override) {
                    foreach (Thruster t in thrusters)
                    {
                        t.TheBlock.Enabled = false;
                        t.IsOn = false;
                    }
                    activeThrusters.Clear();
                }

                foreach (Thruster t in availableThrusters)
                {
                    Base6Directions.Direction thrustForward = t.TheBlock.Orientation.Forward; // Exhaust goes this way

                    if ((thrDir == thrustForward || Override) && ((t.TheBlock.MaxEffectiveThrust != 0 && t.TheBlock.Enabled) || (((!p.parked && p.wgv != 0) || (p.thrustOn && p.wgv == 0)) && !t.TheBlock.Enabled)))
                    {
                        t.TheBlock.Enabled = true;
                        t.IsOn = true;
                        if (!activeThrusters.Contains(t)) activeThrusters.Add(t);
                    }
                }
            }
        }

        class Thruster : BlockWrapper<IMyThrust>
        {

            // stays the same when in standby, if not in standby, this gets updated to weather or not the thruster is on
            public bool IsOn;

            // this indicate the thruster was turned off from the script, and should be kept in the active list
            public bool IsOffBecauseDampeners = true;

            public Thruster(IMyThrust thruster, Program program) : base(thruster, program)
            {
                this.IsOn = false;
                this.TheBlock.Enabled = true;
            }

            // sets the thrust in newtons (N)
            // thrustVec is in worldspace, who'se length is the desired thrust
            public void SetThrust(Vector3D thrustVec)
            {
                SetThrust(thrustVec.Length());
            }

            // sets the thrust in newtons (N)
            public void SetThrust(double thrust)
            {
                TheBlock.ThrustOverride = (float)(thrust.Clamp(0, TheBlock.MaxThrust) * TheBlock.MaxThrust / TheBlock.MaxEffectiveThrust);
            }
        }
        class Rotor : BlockWrapper<IMyMotorStator>
        {
            // don't want IMyMotorBase, that includes wheels

            public Vector3D direction = Vector3D.Zero; //offset relative to the head

            public double LastAngleCos = 0;

            readonly PID pid;

            public bool isHinge;

            int ErrCount = 0;

            public Rotor(IMyMotorStator rotor, Program program) : base(rotor, program)
            {
                p = program;
                pid = new PID(4, 0, 0, 1.0 / 60.0);
                isHinge = TheBlock.BlockDefinition.SubtypeId.Contains("Hinge");
            }

            public double Point(Vector3D requiredVec)
            {
                Vector3D desiredVec = requiredVec.Normalized();
                Vector3D currentDir = Vector3D.TransformNormal(direction, TheBlock.Top.CubeGrid.WorldMatrix);
                double cutoff = p.velprecisionmode * p.force;

                //Better vector pointing system by Whiplash141
                double angleCos = VectorMath.CosBetween(desiredVec, currentDir);
                double angleCosPercent = angleCos * 100;
                double angle = Math.Acos(angleCos) * 2; //previous version was like that (* 2)
                Vector3D axis = Vector3D.Cross(desiredVec, currentDir);
                angle *= Math.Sign(Vector3D.Dot(axis, TheBlock.WorldMatrix.Up)); // angle is the error (facepalm, thanks Whip)

                if (p.EnDebugAPI) {
                    p.Debug.DrawLine(TheBlock.Top.GetPosition(), TheBlock.Top.GetPosition() + currentDir * 2, Color.Cyan, thickness: 0.015f, onTop: true);
                    p.Debug.DrawLine(TheBlock.Top.GetPosition(), TheBlock.Top.GetPosition() + (requiredVec.Normalized() * cutoff) / p.force, Color.Yellow, onTop: true);
                    p.Debug.DrawLine(TheBlock.Top.GetPosition(), TheBlock.Top.GetPosition() + (requiredVec / p.force), Color.Green, onTop: true);
                } 

                if (requiredVec.Length() < cutoff && p.thrustOn)
                {
                    if (((p.wgv == 0 && p.dampeners) || (p.wgv != 0)) && p.thrustOn && Math.Abs(angleCosPercent - LastAngleCos) <= p.ErrorMargin && angleCosPercent < 90)
                    {
                        pid.Kp += p.Aggressivity[0];
                    }
                    else if (angleCosPercent > 98 || Math.Abs(angleCosPercent - LastAngleCos) > p.ErrorMargin)
                    {
                        pid.Kp = p.Aggressivity[0];
                    }
                }
                else if (!p.thrustOn)
                {
                    pid.Kp = p.Aggressivity[1];
                }
                else
                {
                    pid.Kp = p.Aggressivity[2];
                }

                LastAngleCos = angleCosPercent;
                float result = (float)pid.Control(angle);
                

                ///If it's a hinge, and the RPM is the maximum possible, and the cos of angle is -1 (the most far distance), 
                ///it'll asume that hinge is stuck in one of the limits
                if (isHinge) {

                    if (p.ShowMetrics) p.Print($"- {angleCosPercent.Round(4)} - {ErrCount}");

                    if (angleCosPercent <= -99.89) {
                        ErrCount++;
                        if (ErrCount > 10) result = -result;
                    } else if (ErrCount > 10 && angleCosPercent > -98.5) {
                        ErrCount = 0;
                    }
                }

                TheBlock.TargetVelocityRad = result;
                return angleCos;
            }
        }

        class ShipController : BlockWrapper<IMyShipController>
        {
            public bool Dampener;
            public List<IMyThrust> nThrusters = new List<IMyThrust>();
            public List<IMyThrust> cruiseThrusters = new List<IMyThrust>();

            public ShipController(IMyShipController theBlock, Program program) : base(theBlock, program)
            {
                Dampener = theBlock.DampenersOverride;
            }

            public void SetDampener(bool val)
            {
                Dampener = val;
                TheBlock.DampenersOverride = val;
            }
        }

        interface IBlockWrapper
        {
            IMyTerminalBlock TheBlock { get; set; }
            string CName { get; }
        }

        abstract class BlockWrapper<T> : IBlockWrapper where T : class, IMyTerminalBlock
        {
            public T TheBlock { get; set; }

            public Program p;

            public BlockWrapper(T block, Program p)
            {
                this.p = p;
                TheBlock = block;
            }

            // not allowed for some reason
            IMyTerminalBlock IBlockWrapper.TheBlock
            {
                get { return TheBlock; }
                set { TheBlock = (T)value; }
            }

            public string CName => TheBlock.CustomName;
        }
    }
}
