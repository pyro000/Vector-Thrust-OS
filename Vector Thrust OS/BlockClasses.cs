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

            public double thrustModifierAbove = 0.1;// how close the rotor has to be to target position before the thruster gets to full power
            public double thrustModifierBelow = 0.1;// how close the rotor has to be to opposite of target position before the thruster gets to 0 power
            public Vector3D requiredVec = Vector3D.Zero;
            public string Role;

            public float totalEffectiveThrust = 0;

            public int detectThrustCounter = 0;
            public Vector3D currDir = Vector3D.Zero;

            //public double LastAngleCos = 0;
            //float CorrectionRPM = 0;
            //int AngleCosCount = 0;

            //readonly PID pid;
            //float result = 0;

            public VectorThrust(Rotor rotor, Program program)
            {
                this.p = program;
                this.rotor = rotor;
                this.thrusters = new List<Thruster>();
                this.availableThrusters = new List<Thruster>();
                this.activeThrusters = new List<Thruster>();
                Role = GetVTThrRole(program);

                //pid = new PID(4, 0, 0, 1.0 / 60.0);
            }

            // final calculations and setting physical components
            public void Go()
            {
                /*double angleCos;
                double angleCosPercent;*/


                /*if (!p.usepid)
                {
                    // 0.06 135.9
                    double additional = p.sv > 100 ? p.sv / 10 : 1;
                    double correction = 1178 * requiredVec.Length() / p.VTMaxThrust;
                    // 0.07  148

                    angleCos = rotor.SetFromVec(requiredVec);
                    angleCosPercent = angleCos * 100;
                    float iarpm = AI(angleCosPercent, correction / (p.RotorStMultiplier * additional)).NNaN();

                    // 0.077  180 / 0.073 180

                    if (((p.wgv == 0 && p.dampeners) || (p.wgv != 0)) && p.thrustOn && Math.Abs(angleCosPercent - LastAngleCos) <= p.RPMLimit && angleCosPercent < 90)
                    {
                        //if (program.CanPrint()) program.screensb.AppendLine($"TURNING");
                        CorrectionRPM += p.RPMIncrement;
                    }
                    else if (angleCosPercent > 98 || (p.wgv == 0 && iarpm <= 2.5) || Math.Abs(angleCosPercent - LastAngleCos) > p.RPMLimit) //6
                    {
                        CorrectionRPM = 0;
                    }

                    LastAngleCos = angleCosPercent;
                    float finalrpm = CorrectionRPM + iarpm;
                    // 0.08  184

                    if (!p.thrustOn || p.parked)
                    { //This handles rotor speed when parked, in 0G if it's too fast it will turn thrusters back on.
                        double anglecosfixed = angleCosPercent <= 0 ? 1 : angleCosPercent;
                        finalrpm = (float)(0.1 * 17000 / anglecosfixed); // TODO: Find a more precise way to solve this.
                        if (!p.parked) finalrpm = MathHelper.Clamp(finalrpm, 1, (float)p.MaxThrustOffRPM);
                    }

                    rotor.maxRPM = finalrpm;
                }
                else {
                    Vector3D desiredVec = requiredVec.Normalized();
                    Vector3D currentDir = Vector3D.TransformNormal(rotor.direction, rotor.TheBlock.Top.CubeGrid.WorldMatrix);

                    double multipliern = 1;
                    double cutoff = multipliern * p.force;

                    bool reverse = rotor.GetPointOrientation(desiredVec, currentDir);

                    angleCos = rotor.AngleBetweenCos(currentDir, desiredVec, desiredVec.Length());
                    angleCosPercent = angleCos * 100;

                    double rtangle = rotor.TheBlock.Angle;
                    //double angle = rtangle * 180 / Math.PI;
                    double angleRad = Math.Acos(angleCos) * 2;
                    double desiredRad = rtangle - angleRad;
                    double error = (desiredRad - rtangle).NNaN();

                    if (requiredVec.Length() < cutoff && p.thrustOn)
                    {
                        if (((p.wgv == 0 && p.dampeners) || (p.wgv != 0)) && p.thrustOn && Math.Abs(angleCosPercent - LastAngleCos) <= p.RPMLimit && angleCosPercent < 90)
                        {
                            pid.Kp += 0.1;
                        }
                        else if (angleCosPercent > 98 || Math.Abs(angleCosPercent - LastAngleCos) > p.RPMLimit) //6
                        {
                            pid.Kp = 0.1;
                        }
                    }
                    else if (!p.thrustOn)
                    {
                        pid.Kp = 1;
                    }
                    else {
                        pid.Kp = 4;
                    }

                    //if (p.CanPrint()) p.screensb.AppendLine($"A");

                    result = (float)pid.Control(error);
                    rotor.TheBlock.TargetVelocityRad = reverse ? (float)-result : (float)result;
                //}*/

                //LastAngleCos = angleCosPercent;
                // 0.08 189

                double angleCos = rotor.Point(requiredVec);

                // the clipping value 'thrustModifier' defines how far the rotor can be away from the desired direction of thrust, and have the power still at max
                // if 'thrustModifier' is at 1, the thruster will be at full desired power when it is at 90 degrees from the direction of travel
                // if 'thrustModifier' is at 0, the thruster will only be at full desired power when it is exactly at the direction of travel, (it's never exactly in-line)
                // double thrustOffset = (angleCos + 1) / (1 + (1 - Program.thrustModifierAbove));//put it in some graphing calculator software where 'angleCos' is cos(x) and adjust the thrustModifier value between 0 and 1, then you can visualise it
                double abo = thrustModifierAbove;
                double bel = thrustModifierBelow;
                if (abo > 1) { abo = 1; }
                if (abo < 0) { abo = 0; }
                if (bel > 1) { bel = 1; }
                if (bel < 0) { bel = 0; }
                // put it in some graphing calculator software where 'angleCos' is cos(x) and adjust the thrustModifier values between 0 and 1, then you can visualise it
                double thrustOffset = ((((angleCos + 1) * (1 + bel)) / 2) - bel) * (((angleCos + 1) * (1 + abo)) / 2);// the other one is simpler, but this one performs better

                thrustOffset = MathHelper.Clamp(thrustOffset, 0, 1);

                //set the thrust for each engine
                foreach (Thruster thruster in activeThrusters)
                {
                    //Vector3D vec = program.mvin != 0 ? requiredVec.Normalized() * totalEffectiveThrust * program.Accelerations[program.gear] : requiredVec;
                    //program.Echo($"{vec.Length()}/{requiredVec.Length()}");
                    

                    Vector3D thrust = (thrustOffset * requiredVec * thruster.TheBlock.MaxEffectiveThrust / totalEffectiveThrust) + p.residuethrust;
                    /*if (thrust.Length() > 0.1)
                    {
                        program.Echo($"T: {thrust.Length()}");
                        program.Echo($"LEN: {requiredVec.Length()}");
                    }*/
                    bool noThrust = thrust.LengthSquared() < 0.001f || (p.wgv == 0 && angleCos < 0.85);
                    p.tthrust += noThrust ? 0 : MathHelper.Clamp(thrust.Length(), 0, thruster.TheBlock.MaxEffectiveThrust);

                    /*if (thrust.Length() > totalEffectiveThrust) program.residuethrust = thrust.Normalized() * (thrust.Length() - totalEffectiveThrust);
                    else program.residuethrust = Vector3D.Zero;*/

                    //if (program.residuethrust.Length() != 0) program.Echo($"res: {program.residuethrust}");

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
                List<Base6Directions.Axis> cdirs = p.controlledControllers[0].Directions;
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
                                result = "FBUP";
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

                    bool shownAndFunctional = (curr.TheBlock.ShowInTerminal || !p.ignoreHiddenBlocks) && curr.TheBlock.IsFunctional;
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

            /*float AI(double Acos, double VecL)
            {
                List<double> d = p.MagicNumbers;
                double result1 = (Acos * d[0]) + (VecL * d[1]) + d[2];
                double result2 = (Acos * d[3]) + (VecL * d[4]) + d[5];

                double mres1 = Math.Max(0, result1);
                double mres2 = Math.Max(0, result2);

                double sum = (mres1 * d[6]) + (mres2 * d[7]) + d[8];
                return (float)Math.Max(0, sum);
            }*/

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
                rotor.SetPointDir((Vector3D)thrustDir);
                // Base6Directions.Direction rotTopForward = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Forward);
                // Base6Directions.Direction rotTopLeft = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Left);
                // rotor.offset = (float)Math.Acos(rotor.angleBetweenCos(Base6Directions.GetVector(rotTopForward), (Vector3D)thrustDir));

                // disambiguate
                // if(false && Math.Acos(rotor.angleBetweenCos(Base6Directions.GetVector(rotTopLeft), (Vector3D)thrustDir)) > Math.PI/2) {
                // rotor.offset += (float)Math.PI;
                // 	rotor.offset = (float)(2*Math.PI - rotor.offset);
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

            // Depreciated, this is for the old setFromVec
            public float offset = 0;// radians

            
            public Vector3D direction = Vector3D.Zero;//offset relative to the head

            //public string errStr = "";
            public float maxRPM;

            public double LastAngleCos = 0;

            readonly PID pid;

            public Rotor(IMyMotorStator rotor, Program program) : base(rotor, program)
            {
                p = program;

                if (program.maxRotorRPM <= 0)
                {
                    maxRPM = rotor.GetMaximum<float>("Velocity");
                }
                else
                {
                    maxRPM = program.maxRotorRPM;
                }
                pid = new PID(4, 0, 0, 1.0 / 60.0);
            }

            public double Point(Vector3D requiredVec)
            {
                Vector3D desiredVec = requiredVec.Normalized();
                Vector3D currentDir = Vector3D.TransformNormal(direction, TheBlock.Top.CubeGrid.WorldMatrix);

                double multipliern = 1;
                double cutoff = multipliern * p.force;

                bool reverse = GetPointOrientation(desiredVec, currentDir);

                double angleCos = AngleBetweenCos(currentDir, desiredVec/*, desiredVec.Length()*/);
                double angleCosPercent = angleCos * 100;

                double rtangle = TheBlock.Angle;
                double angleRad = Math.Acos(angleCos) * 2;
                double desiredRad = rtangle - angleRad;
                double error = (desiredRad - rtangle).NNaN();

                if (requiredVec.Length() < cutoff && p.thrustOn)
                {
                    if (((p.wgv == 0 && p.dampeners) || (p.wgv != 0)) && p.thrustOn && Math.Abs(angleCosPercent - LastAngleCos) <= p.RPMLimit && angleCosPercent < 90)
                    {
                        pid.Kp += 0.1;
                    }
                    else if (angleCosPercent > 98 || Math.Abs(angleCosPercent - LastAngleCos) > p.RPMLimit)
                    {
                        pid.Kp = 0.1;
                    }
                }
                else if (!p.thrustOn)
                {
                    pid.Kp = 1;
                }
                else
                {
                    pid.Kp = 4;
                }

                LastAngleCos = angleCosPercent;
                float result = (float)pid.Control(error);
                TheBlock.TargetVelocityRad = reverse ? -result : result;
                return angleCos;
            }

            public void SetPointDir(Vector3D dir)
            {
                // MatrixD inv = MatrixD.Invert(theBlock.Top.WorldMatrix);
                // direction = Vector3D.TransformNormal(dir, inv);
                this.direction = dir;
                //TODO: for some reason, this is equal to rotor.worldmatrix.up
            }

            /*===| Part of Rotation By Equinox on the KSH discord channel. |===*/
            private void PointRotorAtVector(IMyMotorStator rotor, Vector3D targetDirection, Vector3D currentDirection, float multiplier)
            {
                double errorScale = Math.PI * maxRPM;
                maxRPM = MathHelper.Clamp(Math.Abs(maxRPM), 0, 60);

                Vector3D angle = Vector3D.Cross(targetDirection, currentDirection);
                // Project onto rotor
                double err = Vector3D.Dot(angle, rotor.WorldMatrix.Up);
                double err2 = Vector3D.Dot(angle.Normalized(), rotor.WorldMatrix.Up);
                double diff = (rotor.WorldMatrix.Up - angle.Normalized()).Length();

                /*this.errStr += $"\nrotor.WorldMatrix.Up: {rotor.WorldMatrix.Up}";
				this.errStr += $"\nangle: {Math.Acos(angleBetweenCos(angle, rotor.WorldMatrix.Up)) * 180.0 / Math.PI}";
				this.errStr += $"\nerr: {err}";
				this.errStr += $"\ndirection difference: {diff}";

				this.errStr += $"\ncurrDir vs Up: {currentDirection.Dot(rotor.WorldMatrix.Up)}";
				this.errStr += $"\ntargetDir vs Up: {targetDirection.Dot(rotor.WorldMatrix.Up)}";

				this.errStr += $"\nmaxRPM: {maxRPM}";
				this.errStr += $"\nerrorScale: {errorScale}";
				this.errStr += $"\nmultiplier: {multiplier}";*/

                double result;

                double rpm = err * errorScale * multiplier;
                //double rpm = err2 * errorScale * multiplier;
                // errStr += $"\nSETTING ROTOR TO {err:N2}";
                if (rpm > maxRPM)
                {
                    result = maxRPM;
                    // this.errStr += $"\nRPM Exceedes Max";
                }
                else if ((rpm * -1) > maxRPM)
                {
                    result = maxRPM * -1;
                    // this.errStr += $"\nRPM Exceedes -Max";
                }
                else
                {
                    result = (float)rpm;
                }

                result = MathHelper.Clamp(result, -p.maxRotorRPM, p.maxRotorRPM);
                result = MathHelper.Clamp(result, -maxRPM, maxRPM);

                //bool cond = (Math.Abs(Vector3.Dot(rotor.WorldMatrix.Up, angle)) > 0.95);

                rotor.TargetVelocityRPM = /*cond ? 0 : */(float)(result.NNaN());
                // this.errStr += $"\nRPM: {(rotor.TargetVelocityRPM).Round(5)}";
            }

            // this sets the rotor to face the desired direction in worldspace
            // desiredVec doesn't have to be in-line with the rotors plane of rotation
            public double SetFromVec(Vector3D desiredVec, float multiplier, bool point = true)
            {
                desiredVec.Normalize();
                //Vector3D currentDir = Vector3D.TransformNormal(this.direction, theBlock.Top.WorldMatrix);
                //                                    only correct if it was built from the head ^ 
                //                                    it needs to be based on the grid
                //this.direction = new Vector3D(direction.X, 0, direction.Z);

                Vector3D currentDir = Vector3D.TransformNormal(this.direction, TheBlock.Top.CubeGrid.WorldMatrix);
                //currentDir = new Vector3D(currentDir.X, 0, currentDir.Z);


                if (point) PointRotorAtVector(TheBlock, desiredVec, currentDir/*theBlock.Top.WorldMatrix.Forward*/, multiplier);

                return AngleBetweenCos(currentDir, desiredVec/*, desiredVec.Length()*/);
            }

            public bool GetPointOrientation(Vector3D targetDirection, Vector3D currentDirection)
            {
                Vector3D angle = Vector3D.Cross(targetDirection, currentDirection);
                double err = Vector3D.Dot(angle, TheBlock.WorldMatrix.Up);
                return err >= 0;
            }

            public double SetFromVec(Vector3D desiredVec, bool point = true)
            {
                return SetFromVec(desiredVec, 1, point);
            }

            // gets cos(angle between 2 vectors)
            // cos returns a number between 0 and 1
            // use Acos to get the angle
            //THIS COULD BE NECESSARY IN SOME FUTURE.....
            /*public double AngleBetweenCos(Vector3D a, Vector3D b)
            {
                double dot = Vector3D.Dot(a, b);
                double Length = a.Length() * b.Length();
                return dot / Length;
            }*/

            // gets cos(angle between 2 vectors)
            // cos returns a number between 0 and 1
            // use Acos to get the angle
            // doesn't calculate length because thats expensive
            public double AngleBetweenCos(Vector3D a, Vector3D b/*, double len_a_times_len_b*/)
            {
                double dot = Vector3D.Dot(a, b);
                return dot / /*len_a_times_len_b*/b.Length();
            }

            // set the angle to be between 0 and 2pi radians (0 and 360 degrees)
            // this takes and returns radians
            /*float cutAngle(float angle)
			{
				while (angle > Math.PI)
				{
					angle -= 2 * (float)Math.PI;
				}
				while (angle < -Math.PI)
				{
					angle += 2 * (float)Math.PI;
				}
				return angle;
			}*/
        }
        class ShipController : BlockWrapper<IMyShipController>
        {
            public bool Dampener;


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

            public string CName { get; }

            public Program p;

            public BlockWrapper(T block, Program p)
            {
                this.p = p;
                TheBlock = block;
                Directions = GetDirections(block);
                //CName = block.CustomName;
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

            string IBlockWrapper.CName
            {
                get { return TheBlock.CustomName; }
            }
        }
    }
}
