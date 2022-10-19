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
        //int colorid = 0;
        //List<Color> colors = new List<Color> { Color.White, Color.Cyan, Color.Green, Color.Yellow, Color.Red, Color.Blue, Color.Pink };

        /*public static Vector3D Projection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return Vector3D.Zero;

            if (Vector3D.IsUnit(ref b))
                return a.Dot(b) * b;

            return a.Dot(b) / b.LengthSquared() * b;
        }*/

        class VectorThrust
        {
            readonly Program p;

            // physical parts
            public Rotor rotor;
            public List<Thruster> thrusters;// all the thrusters
            public List<Thruster> availableThrusters;// <= thrusters: the ones the user chooses to be used (ShowInTerminal)
            public List<Thruster> activeThrusters;// <= activeThrusters: the ones that are facing the direction that produces the most thrust (only recalculated if available thrusters changes)

            public Vector3D requiredVec = Vector3D.Zero;
            //public string Role;

            public float totalEffectiveThrust = 0;

            public int detectThrustCounter = 0;
            public Vector3D currDir = Vector3D.Zero;
            //bool isassigned = false;

            public VectorThrust(Rotor rotor, Program program)
            {
                this.p = program;
                this.rotor = rotor;
                this.thrusters = new List<Thruster>();
                this.availableThrusters = new List<Thruster>();
                this.activeThrusters = new List<Thruster>();

                //Role = AssignRole(); //GetVTThrRole(/*program*/);
                //p.temp1++;
            }

            // final calculations and setting physical components
            public void Go()
            {
                // 0.08 189
                double angleCos = rotor.Point(requiredVec/*, totalEffectiveThrust*/);
                
                // the clipping value 'thrustModifier' defines how far the rotor can be away from the desired direction of thrust, and have the power still at max
                // if 'thrustModifier' is at 1, the thruster will be at full desired power when it is at 90 degrees from the direction of travel
                // if 'thrustModifier' is at 0, the thruster will only be at full desired power when it is exactly at the direction of travel, (it's never exactly in-line)

                double /*abo*/tmod = MathHelper.Clamp(p.thrustermodifier, 0, 1); //Temporal
                //double bel = abo;

                // put it in some graphing calculator software where 'angleCos' is cos(x) and adjust the thrustModifier values between 0 and 1, then you can visualise it
                double thrustOffset = ((((angleCos + 1) * (1 + /*bel*/tmod)) / 2) - /*bel*/tmod) * (((angleCos + 1) * (1 + /*abo*/tmod)) / 2);// the other one is simpler, but this one performs better
                thrustOffset = MathHelper.Clamp(thrustOffset, 0, 1);

                //set the thrust for each engine
                foreach (Thruster thruster in activeThrusters)
                {
                    Vector3D thrust = (thrustOffset * requiredVec * thruster.TheBlock.MaxEffectiveThrust / totalEffectiveThrust);// + p.residuethrust;
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

                Base6Directions.Axis raxis = Base6Directions.GetAxis(this.rotor.TheBlock.Orientation.Up);

                foreach (List<VectorThrust> g in p.VTThrGroups)
                {
                    Base6Directions.Axis gaxis = Base6Directions.GetAxis(g[0].rotor.TheBlock.Orientation.Up);
                    if (gaxis.Equals(raxis)/*this.Role == g[0].Role*/)
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

            //*string */ void GetVTThrRole(/*Program p*/)
            /*public /*voidstring AssignRole()
            {
                if (isassigned) return Role;
                isassigned = true;

                string result = "";
                List<Base6Directions.Axis> cdirs = p.mainController.Axis;
                List<Base6Directions.Axis> rdirs = rotor.Axis;

                //if (!rotor.isHinge || true)
                //{
                    for (int i = 0; i < cdirs.Count; i++)
                    {
                        if (cdirs[i] == rdirs[1])
                        {
                            switch (i)
                            {
                                case 0:
                                //Echo("front/back mounted, rotor covers cockpit's up/down/left/right");
                                result = "UDLR";
                                //p.rolecount[1]++; 
                                //p.rolecount[2]++; 
                                break;
                            case 1:
                                //Echo("top/bottom mounted, rotor covers cockpit's forward/back/left/right");
                                result = "FBLR";
                                //p.rolecount[0]++; 
                                //p.rolecount[2]++; 
                                break;
                                case 2:
                                //Echo("side mounted, rotor covers cockpit's forward/back/up/down");
                                result = "FBUD";
                                //p.rolecount[0]++; 
                                //p.rolecount[1]++;  
                                break;
                            }
                        }
                    }
                /*}
                else
                {
                    List<Vector3D> csdirs = p.mainController.SpDirections;
                    List<Vector3D> rsdirs = rotor.SpDirections;

                    for (int i = 0; i < cdirs.Count; i++)
                    
                        if (cdirs[i] == rdirs[0])
                        {
                            switch (i)
                            {
                                case 0:
                                    result = "FB"; break;
                                case 1:
                                    result = "UD"; break;
                                case 2:
                                    result = "LR"; break;
                            }
                        }

                    for (int i = 0; i < csdirs.Count; i++)
                        if (csdirs[i] == rsdirs[5])
                    {
                            

                        switch (i)
                        {
                            case 0:
                                result += "B"; break;
                            case 1:
                                result += "D"; break;
                            case 2:
                                result += "R"; break;
                            case 3:
                                result += "F"; break;
                            case 4:
                                result += "U"; break;
                            case 5:
                                result += "L"; break;
                        }
                    }


                }*/
                //return result;
                /*p.rolemaxcount = new List<int>(p.rolecount);
                p.rolemaxcount.Sort();

                bool basic1 = p.rolemaxcount[2] <= 2;
                bool basic2 = p.rolemaxcount[1] == 1;

                bool inter1 = p.rolemaxcount[2] >= 3;
                bool inter2 = p.rolemaxcount[1] >= 2;

                return result;
                //p.rolesum = basic1 ? 2 : inter1 && inter2 ? p.rolemaxcount[2] + p.rolemaxcount[1] : p.rolemaxcount[2];
            }*/

            

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
                //if (thrusters.All(x => !x.TheBlock.Enabled)) p.temp1++;

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
                }
                else if (thrust < 0)
                {
                    thrust = 0;
                }

                TheBlock.ThrustOverride = (float)(thrust * TheBlock.MaxThrust / TheBlock.MaxEffectiveThrust);
            }
        }
        class Rotor : BlockWrapper<IMyMotorStator>
        {
            // don't want IMyMotorBase, that includes wheels

            public Vector3D direction = Vector3D.Zero; //offset relative to the head

            public double LastAngleCos = 0;

            readonly PID pid;

            public bool isHinge;

            public bool reachededge = false;

            public Rotor(IMyMotorStator rotor, Program program) : base(rotor, program)
            {
                p = program;
                pid = new PID(4, 0, 0, 1.0 / 60.0);
                isHinge = TheBlock.BlockDefinition.SubtypeId.Contains("Hinge");
            }

            public double Point(Vector3D requiredVec/*, double efthr*/)
            {
                Vector3D desiredVec = requiredVec.Normalized();
                Vector3D currentDir = Vector3D.TransformNormal(direction, TheBlock.Top.CubeGrid.WorldMatrix);
                double cutoff = p.velprecisionmode * p.force;

                //Better vector pointing system by Whiplash141
                //Vector3D currentDirectionFlat = VectorMath.Rejection(currentDir, TheBlock.WorldMatrix.Up);
                double angleCos = VectorMath.CosBetween(desiredVec, /*currentDirectionFlat*/currentDir);
                double angleCosPercent = angleCos * 100;
                double angle = Math.Acos(angleCos) * 2; //previous version was like that (* 2)
                Vector3D axis = Vector3D.Cross(desiredVec, /*currentDirectionFlat*/currentDir);
                //p.Echo($"3 {axis}");
                angle *= Math.Sign(Vector3D.Dot(axis, TheBlock.WorldMatrix.Up)); // angle is the error (facepalm, thanks Whip)
                //p.Echo($"4 {angle}");

                //if (p.EnDebugAPI) {
                    p.Debug.DrawLine(TheBlock.Top.GetPosition(), TheBlock.Top.GetPosition() + currentDir * 2, Color.Cyan, thickness: 0.015f, onTop: true);
                    p.Debug.DrawLine(TheBlock.Top.GetPosition(), TheBlock.Top.GetPosition() + (requiredVec.Normalized() * cutoff) / p.force, Color.Yellow, onTop: true);
                    //p.Debug.DrawLine(TheBlock.Top.GetPosition(), TheBlock.Top.GetPosition() + (requiredVec / p.force), Color.Green, onTop: true);
                //} 

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
                float result = (float)pid.Control(/*error*/angle);

                //if (angleCosPercent.Round(2) == -100 && result)
                double currentrpm = (TheBlock.TargetVelocityRad * 9.549297).Round(2).Abs();
                double maxrpm = TheBlock.GetMaximum<float>("Velocity");
                double tempp = angleCosPercent.Round(2);

                p.Print($"- {tempp} - {currentrpm} {maxrpm}");

                ///If it's a hinge, and the RPM is the maximum possible, and the cos of angle is -1 (the most far distance), 
                ///it'll asume that hinge is stuck in one of the limits
                if (isHinge && tempp == -100 && currentrpm == maxrpm) { 
                    //result *= -1;
                    if (!reachededge)
                    {
                        reachededge = true;
                    }
                    else {
                        //p.Print("I'M STUCK STEPROTOR");
                        result = -result;
                    }
                } else if (isHinge && reachededge && tempp > -98.5) {
                    reachededge = false;
                }

                p.Print($"2 {(result * 9.549297).Round(2)}");
                TheBlock.TargetVelocityRad = result;//!reverse ? -result : result;
                return angleCos;
            }

            /*public bool GetPointOrientation(Vector3D targetDirection, Vector3D currentDirection)
            {
                Vector3D angle = Vector3D.Cross(targetDirection, currentDirection);
                double err = Vector3D.Dot(angle.Normalized(), (TheBlock.WorldMatrix.Up + TheBlock.WorldMatrix.Left).Normalized());
                return err >= 0 /*&& cond;
            }*/

            // doesn't calculate length because thats expensive
            /*public double AngleBetweenCos(Vector3D a, Vector3D b)
            {
                //double dot = Vector3D.Dot(a, b);
                return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);//dot / b.Length();
            }*/

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
            //List<Base6Directions.Axis> Axis { get; }
            //List<Vector3D> SpDirections { get; }
            string CName { get; }
        }

        abstract class BlockWrapper<T> : IBlockWrapper where T : class, IMyTerminalBlock
        {
            public T TheBlock { get; set; }

            //public List<Base6Directions.Axis> Axis { get; }
            //public List<Vector3D> SpDirections { get; }

            public Program p;

            public BlockWrapper(T block, Program p)
            {
                this.p = p;
                TheBlock = block;
                //Axis = GetAxis(block);
                //SpDirections = GetSpecialDirections(block);
            }

            // not allowed for some reason
            IMyTerminalBlock IBlockWrapper.TheBlock
            {
                get { return TheBlock; }
                set { TheBlock = (T)value; }
            }

            /*public List<Base6Directions.Axis> GetAxis(IMyTerminalBlock block)
            {
                MyBlockOrientation o = block.Orientation;
                return new List<Base6Directions.Axis> { Base6Directions.GetAxis(o.Forward), Base6Directions.GetAxis(o.Up), Base6Directions.GetAxis(o.Left) };
            }*/

            /*public List<Vector3D> GetSpecialDirections(IMyTerminalBlock block)
            {
                return new List<Vector3D> { block.WorldMatrix.Forward, block.WorldMatrix.Up, block.WorldMatrix.Left, block.WorldMatrix.Backward, block.WorldMatrix.Down, block.WorldMatrix.Right };
            }*/
            public string CName => TheBlock.CustomName;
        }
    }
}
