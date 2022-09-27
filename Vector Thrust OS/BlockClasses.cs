using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        //bool usepid = true;

        class VectorThrust
        {
            readonly Program p;

            // physical parts
            public Rotor rotor;
            public List<Thruster> thrusters;// all the thrusters
            public List<Thruster> availableThrusters;// <= thrusters: the ones the user chooses to be used (ShowInTerminal)
            public List<Thruster> activeThrusters;// <= activeThrusters: the ones that are facing the direction that produces the most thrust (only recalculated if available thrusters changes)

            /*public double thrustModifierAbove = 0.1;// how close the rotor has to be to target position before the thruster gets to full power
            public double thrustModifierBelow = 0.1;// how close the rotor has to be to opposite of target position before the thruster gets to 0 power
            */public Vector3D requiredVec = Vector3D.Zero;
            public string Role;

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
                Role = GetVTThrRole(program);
            }

            // final calculations and setting physical components
            public void Go()
            {
                // 0.08 189
                double angleCos = rotor.Point(requiredVec);

                // the clipping value 'thrustModifier' defines how far the rotor can be away from the desired direction of thrust, and have the power still at max
                // if 'thrustModifier' is at 1, the thruster will be at full desired power when it is at 90 degrees from the direction of travel
                // if 'thrustModifier' is at 0, the thruster will only be at full desired power when it is exactly at the direction of travel, (it's never exactly in-line)
                // double thrustOffset = (angleCos + 1) / (1 + (1 - Program.thrustModifierAbove));//put it in some graphing calculator software where 'angleCos' is cos(x) and adjust the thrustModifier value between 0 and 1, then you can visualise it
                /*double abo = thrustModifierAbove;
                double bel = thrustModifierBelow;*/

                double abo = MathHelper.Clamp(p.thrustermodifier, 0, 1);
                double bel = abo;

                /*if (abo > 1) { abo = 1; }
                if (abo < 0) { abo = 0; }
                if (bel > 1) { bel = 1; }
                if (bel < 0) { bel = 0; }*/
                // put it in some graphing calculator software where 'angleCos' is cos(x) and adjust the thrustModifier values between 0 and 1, then you can visualise it
                double thrustOffset = ((((angleCos + 1) * (1 + bel)) / 2) - bel) * (((angleCos + 1) * (1 + abo)) / 2);// the other one is simpler, but this one performs better

                thrustOffset = MathHelper.Clamp(thrustOffset, 0, 1);

                //set the thrust for each engine
                foreach (Thruster thruster in activeThrusters)
                {
                    Vector3D thrust = (thrustOffset * requiredVec * thruster.TheBlock.MaxEffectiveThrust / totalEffectiveThrust) + p.residuethrust;
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
            }

            public void AssignGroup()
            {
                bool foundGroup = false;

                foreach (List<VectorThrust> g in p.VTThrGroups)
                {
                    if (this.Role == g[0].Role)
                    {
                        if (!g.Contains(this)) g.Add(this);
                        foundGroup = true;
                        break;
                    }
                }
                if (!foundGroup)
                {// if it never found a group, add a group
                    p.VTThrGroups.Add(new List<VectorThrust>());
                    p.VTThrGroups[p.VTThrGroups.Count - 1].Add(this);
                }
            }

            public void CalcTotalEffectiveThrust()
            {
                totalEffectiveThrust = 0;
                foreach (Thruster t in activeThrusters)
                {
                    totalEffectiveThrust += t.TheBlock.MaxEffectiveThrust;
                }
            }

            string GetVTThrRole(Program p)
            {
                string result = "";
                List<Base6Directions.Axis> cdirs = p./*controlledControllers[0]*/mainController.Directions;
                List<Base6Directions.Axis> rdirs = rotor.Directions;

                for (int i = 0; i < cdirs.Count; i++)
                {
                    if (cdirs[i] == rdirs[1])
                    {
                        switch (i)
                        {
                            case 0:
                                //Echo("front/back mounted, rotor covers cockpit's up/down/left/right");
                                result = "UDLR";
                                break;
                            case 1:
                                result = "FBLR";
                                //Echo("top/bottom mounted, rotor covers cockpit's forward/back/left/right");
                                break;
                            case 2:
                                result = "FBUD";
                                //Echo("side mounted, rotor covers cockpit's forward/back/up/down");
                                break;
                        }
                    }
                }
                return result;
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
                    // DTerrStr += $"\nengineDirection.X";
                    thrustDir.X = 1;
                }
                else if (Math.Abs(maxAbs - engineDirection.Y) < DELTA)
                {
                    // DTerrStr += $"\nengineDirection.Y";
                    thrustDir.Y = 1;
                }
                else if (Math.Abs(maxAbs - engineDirection.Z) < DELTA)
                {
                    // DTerrStr += $"\nengineDirection.Z";
                    thrustDir.Z = 1;
                }
                else if (Math.Abs(maxAbs - engineDirectionNeg.X) < DELTA)
                {
                    // DTerrStr += $"\nengineDirectionNeg.X";
                    thrustDir.X = -1;
                }
                else if (Math.Abs(maxAbs - engineDirectionNeg.Y) < DELTA)
                {
                    // DTerrStr += $"\nengineDirectionNeg.Y";
                    thrustDir.Y = -1;
                }
                else if (Math.Abs(maxAbs - engineDirectionNeg.Z) < DELTA)
                {
                    // DTerrStr += $"\nengineDirectionNeg.Z";
                    thrustDir.Z = -1;
                }
                else
                {
                    // DTerrStr += $"\nERROR (detectThrustDirection):\nmaxAbs doesn't match any engineDirection\n{maxAbs}\n{engineDirection}\n{engineDirectionNeg}";
                    return;
                }

                // use thrustDir to set rotor offset
                rotor.direction = (Vector3D)thrustDir;
                // Base6Directions.Direction rotTopForward = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Forward);
                // Base6Directions.Direction rotTopLeft = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Left);
                // rotor.offset = (float)Math.Acos(rotor.angleBetweenCos(Base6Directions.GetVector(rotTopForward), (Vector3D)thrustDir));

                // disambiguate
                // if(false && Math.Acos(rotor.angleBetweenCos(Base6Directions.GetVector(rotTopLeft), (Vector3D)thrustDir)) > Math.PI/2) {
                // rotor.offset += (float)Math.PI;
                // rotor.offset = (float)(2*Math.PI - rotor.offset);
                // }

                // put thrusters into the active list
                Base6Directions.Direction thrDir = Base6Directions.GetDirection(thrustDir);
                ActiveList(thrDir);

                //}
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
                //if (program.thrustOn) { //IDK IF THIS DOES SOMETHING USEFUL
                foreach (Thruster t in availableThrusters)
                {
                    Base6Directions.Direction thrustForward = t.TheBlock.Orientation.Forward; // Exhaust goes this way

                    if ((thrDir == thrustForward || Override) && ((t.TheBlock.MaxEffectiveThrust != 0 && t.TheBlock.Enabled) || (!p.parked && !t.TheBlock.Enabled)))
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
                // this.IsOn = theBlock.Enabled;
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

                if (thrust > TheBlock.MaxThrust)
                {
                    thrust = TheBlock.MaxThrust;
                    // errStr += $"\nExceeding max thrust";
                }
                else if (thrust < 0)
                {
                    // errStr += $"\nNegative Thrust";
                    thrust = 0;
                }

                TheBlock.ThrustOverride = (float)(thrust * TheBlock.MaxThrust / TheBlock.MaxEffectiveThrust);
                /*errStr += $"\nEffective {(100*theBlock.MaxEffectiveThrust / theBlock.MaxThrust).Round(1)}%";
				errStr += $"\nOverride {theBlock.ThrustOverride}N";*/
            }
        }
        class Rotor : BlockWrapper<IMyMotorStator>
        {
            // don't want IMyMotorBase, that includes wheels

            public Vector3D direction = Vector3D.Zero; //offset relative to the head

            //public float maxRPM;

            public double LastAngleCos = 0;

            readonly PID pid;

            public Rotor(IMyMotorStator rotor, Program program) : base(rotor, program)
            {
                p = program;

                /*if (program.maxRotorRPM <= 0)
                {
                    maxRPM = rotor.GetMaximum<float>("Velocity");
                }
                else
                {
                    maxRPM = program.maxRotorRPM;
                }*/
                pid = new PID(4, 0, 0, 1.0 / 60.0);
            }

            public double Point(Vector3D requiredVec)
            {
                Vector3D desiredVec = requiredVec.Normalized();
                Vector3D currentDir = Vector3D.TransformNormal(direction, TheBlock.Top.CubeGrid.WorldMatrix);

                double cutoff = p.velprecisionmode * p.force;
                bool reverse = GetPointOrientation(desiredVec, currentDir);

                double angleCos = AngleBetweenCos(currentDir, desiredVec);
                double angleCosPercent = angleCos * 100;

                double rtangle = TheBlock.Angle;
                double angleRad = Math.Acos(angleCos) * 2;
                double desiredRad = rtangle - angleRad;
                double error = (desiredRad - rtangle).NNaN();

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
                    pid.Kp = p.Aggressivity[1];//!p.parked && p.wgv == 0 ? 1 : 4;
                }
                else
                {
                    pid.Kp = p.Aggressivity[2];
                }

                LastAngleCos = angleCosPercent;
                float result = (float)pid.Control(error);
                TheBlock.TargetVelocityRad = reverse ? -result : result;
                return angleCos;
            }

            public bool GetPointOrientation(Vector3D targetDirection, Vector3D currentDirection)
            {
                Vector3D angle = Vector3D.Cross(targetDirection, currentDirection);
                double err = Vector3D.Dot(angle, TheBlock.WorldMatrix.Up);
                return err >= 0;
            }

            // doesn't calculate length because thats expensive
            public double AngleBetweenCos(Vector3D a, Vector3D b/*, double len_a_times_len_b*/)
            {
                double dot = Vector3D.Dot(a, b);
                return dot / /*len_a_times_len_b*/b.Length();
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
                //program.controllerblocks.Add(theBlock);
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
            List<Base6Directions.Axis> Directions { get; }

            string CName { get; }
        }

        abstract class BlockWrapper<T> : IBlockWrapper where T : class, IMyTerminalBlock
        {
            public T TheBlock { get; set; }

            public List<Base6Directions.Axis> Directions { get; }

            //public string CName { get; }

            public Program p;

            public BlockWrapper(T block, Program p)
            {
                this.p = p;
                TheBlock = block;
                Directions = GetDirections(block);
            }

            // not allowed for some reason
            //public static implicit operator IMyTerminalBlock(BlockWrapper<T> wrap) => wrap.theBlock;

            IMyTerminalBlock IBlockWrapper.TheBlock
            {
                get { return TheBlock; }
                set { TheBlock = (T)value; }
            }

            public List<Base6Directions.Axis> GetDirections(IMyTerminalBlock block)
            {
                MyBlockOrientation o = block.Orientation;
                return new List<Base6Directions.Axis> { Base6Directions.GetAxis(o.Forward), Base6Directions.GetAxis(o.Up), Base6Directions.GetAxis(o.Left) };
            }

            public string CName => TheBlock.CustomName;
        }
    }
}
