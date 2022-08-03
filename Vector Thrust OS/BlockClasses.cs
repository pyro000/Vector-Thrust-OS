using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {

        class VectorThrust
        {
            public Program program;
            //public readonly PID pid = new PID(1, 0, 0, (1.0 / 60.0));
            //readonly PidController c = new PidController(25, 0, 0.1, 60, -60);
            //Lag avg;

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

            public double totalmaxthrust = 0;

            public int detectThrustCounter = 0;
            public Vector3D currDir = Vector3D.Zero;

            public double old_angleCos = 0;
            float CorrectionRPM = 0;
            int AngleCosCount = 0;
            //int avgsamples;

            //public Nacelle() { }// don't use this if it is possible for the instance to be kept (Not necessary)
            public VectorThrust(Rotor rotor, Program program)
            {
                this.program = program;
                this.rotor = rotor;
                this.thrusters = new List<Thruster>();
                this.availableThrusters = new List<Thruster>();
                this.activeThrusters = new List<Thruster>();
                Role = GetVTThrRole(program);
                //this.avgsamples = program.RotationAverageSamples;
                //this.avg = new Lag(this.avgsamples);
            }

            // final calculations and setting physical components
            public void Go()
            {
                // 134 0.068

                /*if (avgsamples != program.RotationAverageSamples)
				{
					avgsamples = program.RotationAverageSamples;
					avg = new Lag(avgsamples);
				}*/

                // 0.06 135.9

                /*totalEffectiveThrust = */
                CalcTotalEffectiveThrust(/*activeThrusters*/);
                double maxlength = MathHelper.Clamp(requiredVec.Length(), 0, 15000);
                // 0.07  148
                double multiplier = totalEffectiveThrust * 29.167 / program.myshipmass.PhysicalMass;

                double accel = program.maxaccel * program.myshipmass.PhysicalMass;

                double additional = program.sv > 100 ? program.sv / 4 : 1;

                //program.screensb.AppendLine("vel: " + additional);

                double correction = 4714.285714 * requiredVec.Length() / accel;
                //program.screensb.AppendLine("x:" + accel.Round(0) + " / " + correction.Round(0));


                //bool usepid = (program.parked && program.UsePIDPark) || program.UsePID;
                //requiredVec = new Vector3D(requiredVec.X, 0, requiredVec.Z);
                //program.Write();

                double angleCos = rotor.SetFromVec(requiredVec);
                double angleCosPercent = angleCos * 100;

                //double req = requiredVec.Length();
                //req /= program.mvin != 0 && angleCosPercent > 90 ? program.sv/50 : 1;

                //bool dampeners = program.dampeners;
                //bool TO = program.thrustOn;
                //bool cruise = program.cruise;
                //bool nthr = program.normalThrusters != null;
                //bool movement = program.mvin > 0;
                //bool slowThrustOff = program.SlowThrustOff;
                //bool park = program.parked && program.alreadyparked && program.setTOV;
                //double rVecLength = requiredVec.Length();
                //double multiplier = program.RotorStMultiplier;
                //float MaxRPM = program.maxRotorRPM;
                //float STval = (float)program.MaxThrustOffRPM;
                float iarpm = AI(angleCosPercent, correction / (program.RotorStMultiplier * additional)).NNaN();

                // 0.077  180 / 0.073 180
                //return;

                //TODO: MAKE IT WORK WITH PARK AND STOP, WORKS NOW
                //if (!usepid)
                //{
                //if (program.wgv == 0) {
                if (/*!program.normalThrusters.Empty() &&*/ program.dampeners && !program.cruise /*&& program.mvin == 0*/ && program.thrustOn && Math.Abs(angleCosPercent - old_angleCos) <= 15 && angleCosPercent < 95 && iarpm < 5) AngleCosCount++;
                else AngleCosCount = 0;
                //}

                //if (program.wgv == 0) { 
                if (AngleCosCount > 60 && angleCosPercent < 95) CorrectionRPM += iarpm /*program.wgv == 0 ? AngleCosCount : 15*/;
                else if (angleCosPercent > 95/* || iarpm <= 2.5*/) CorrectionRPM = 0;
                old_angleCos = angleCosPercent;
                //}
                //}

                // 0.077 180

                //double rtangle = rotor.TheBlock.Angle;
                //double angle = rtangle * 180 / Math.PI;
                //double angleRad = Math.Acos(angleCos) * 2;
                //double desiredRad = rtangle - angleRad;
                //double error = (desiredRad - rtangle).NNaN();
                //float result = (float)pid.Control(error);

                //I can't get PID to work, using it only to handle parking
                //program.screensb.AppendLine("rs: " + result.Round(2) + " drad: " + desiredRad.Round(2));
                //program.screensb.AppendLine("er:"+error);
                /*float dif = result + 3.1416f;
				if (dif < 0) {
					result = Math.Abs(dif);
				}*/
                //program.Echo("frpm: " + iarpm);

                //program.Write(rotor.CName);
                //program.Write("a:" + requiredVec.Round(0));



                //if (!usepid)
                //{
                float finalrpm = CorrectionRPM + iarpm;
                //avg.Update(truerpm);
                //float finalrpm = (float)avg.Value;

                // 0.08  184


                if (!program.thrustOn || program.parked)
                {
                    double anglecosfixed = angleCosPercent <= 0 ? 1 : angleCosPercent;
                    finalrpm = (float)(0.1 * 17000 / anglecosfixed); // TODO: Find a more precise way to solve this.
                    if (!program.parked) finalrpm = MathHelper.Clamp(finalrpm, 1, (float)program.MaxThrustOffRPM);
                }

                //rotor.maxRPM = (TO || program.parked) ? finalrpm : ((!TO && !program.parked && (slowThrustOff || cruise))/* || (program.parked)*/ ? STval : finalrpm);
                rotor.maxRPM = finalrpm;

                // 0.08 189
                //}
                //else {
                //float test = RotateThrusterTowards(requiredVec, thrusters[0].TheBlock, rotor.TheBlock);
                //rotor.TheBlock.TargetVelocityRad = Math.Abs(error) < 0.01 ? 0 : result;
                //}

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
                                                                                                                      // double thrustOffset = (angleCos * (1 + abo) * (1 + bel) + abo - bel + 1) / 2;
                /*if (thrustOffset > 1)
				{
					thrustOffset = 1;
				}
				else if (thrustOffset < 0)
				{
					thrustOffset = 0;
				}*/

                thrustOffset = MathHelper.Clamp(thrustOffset, 0, 1);

                //set the thrust for each engine
                foreach (Thruster thruster in activeThrusters)
                {
                    //program.screensb.AppendLine("tr:" + thruster.TheBlock.MaxEffectiveThrust);

                    // errStr += thrustOffset.progressBar();
                    Vector3D thrust = thrustOffset * requiredVec * thruster.TheBlock.MaxEffectiveThrust / totalEffectiveThrust;
                    bool noThrust = thrust.LengthSquared() < 0.001f || (program.wgv == 0 && angleCosPercent < 85);
                    program.tthrust += noThrust ? 0 : MathHelper.Clamp(thrust.Length(), 0, thruster.TheBlock.MaxEffectiveThrust);



                    if (/*!jetpack || */!program.thrustOn || noThrust)
                    {
                        thruster.SetThrust(0);
                        thruster.TheBlock.Enabled = false;
                        thruster.IsOffBecauseDampeners = !program.thrustOn || noThrust;
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

                foreach (List<VectorThrust> g in program.VTThrGroups)
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
                    program.VTThrGroups.Add(new List<VectorThrust>());
                    program.VTThrGroups[program.VTThrGroups.Count - 1].Add(this);
                }
            }

            public void CalcTotalEffectiveThrust(/*IEnumerable<Thruster> thrusters*/)
            {
                totalEffectiveThrust = 0;
                foreach (Thruster t in /*thrusters*/activeThrusters)
                {
                    totalEffectiveThrust += t.TheBlock.MaxEffectiveThrust;
                    totalmaxthrust += t.TheBlock.MaxThrust;
                }
                //return total;
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

                    bool shownAndFunctional = (curr.TheBlock.ShowInTerminal || !program.ignoreHiddenBlocks) && curr.TheBlock.IsFunctional;
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

            float AI(double Acos, double VecL)
            {
                List<double> d = program.MagicNumbers;
                double result1 = (Acos * d[0]) + (VecL * d[1]) + d[2];
                double result2 = (Acos * d[3]) + (VecL * d[4]) + d[5];

                double mres1 = Math.Max(0, result1);
                double mres2 = Math.Max(0, result2);

                double sum = (mres1 * d[6]) + (mres2 * d[7]) + d[8];
                return (float)Math.Max(0, sum);
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
                rotor.SetPointDir((Vector3D)thrustDir);
                // Base6Directions.Direction rotTopForward = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Forward);
                // Base6Directions.Direction rotTopLeft = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Left);
                // rotor.offset = (float)Math.Acos(rotor.angleBetweenCos(Base6Directions.GetVector(rotTopForward), (Vector3D)thrustDir));

                // disambiguate
                // if(false && Math.Acos(rotor.angleBetweenCos(Base6Directions.GetVector(rotTopLeft), (Vector3D)thrustDir)) > Math.PI/2) {
                // rotor.offset += (float)Math.PI;
                // 	rotor.offset = (float)(2*Math.PI - rotor.offset);
                // }

                foreach (Thruster t in thrusters)
                {
                    t.TheBlock.Enabled = false;
                    t.IsOn = false;
                }
                activeThrusters.Clear();

                // put thrusters into the active list
                Base6Directions.Direction thrDir = Base6Directions.GetDirection(thrustDir);
                //if (program.thrustOn) { //IDK IF THIS DOES SOMETHING USEFUL
                foreach (Thruster t in availableThrusters)
                {
                    Base6Directions.Direction thrustForward = t.TheBlock.Orientation.Forward; // Exhaust goes this way

                    if (thrDir == thrustForward && ((t.TheBlock.MaxEffectiveThrust != 0 && t.TheBlock.Enabled) || !t.TheBlock.Enabled))
                    {
                        t.TheBlock.Enabled = true;
                        t.IsOn = true;
                        activeThrusters.Add(t);
                    }
                }
                //}
            }

        }

        class Thruster : BlockWrapper<IMyThrust>
        {

            // stays the same when in standby, if not in standby, this gets updated to weather or not the thruster is on
            public bool IsOn;

            // this indicate the thruster was turned off from the script, and should be kept in the active list
            public bool IsOffBecauseDampeners = true;

            public Thruster(IMyThrust thruster) : base(thruster)
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

            public Program program;
            public Vector3D direction = Vector3D.Zero;//offset relative to the head

            //public string errStr = "";
            public float maxRPM;

            public Rotor(IMyMotorStator rotor, Program program) : base(rotor)
            {
                this.program = program;

                if (program.maxRotorRPM <= 0)
                {
                    maxRPM = rotor.GetMaximum<float>("Velocity");
                }
                else
                {
                    maxRPM = program.maxRotorRPM;
                }
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

                result = MathHelper.Clamp(result, -program.maxRotorRPM, program.maxRotorRPM);
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
                //errStr = "";
                //desiredVec = desiredVec.reject(theBlock.WorldMatrix.Up);
                //this.errStr += $"\ncurrent dir: {currentDir}\ntarget dir: {desiredVec}\ndiff: {currentDir - desiredVec}";
                //Vector3D currentDir = Vector3D.TransformNormal(this.direction, theBlock.Top.WorldMatrix);
                //                                    only correct if it was built from the head ^ 
                //                                    it needs to be based on the grid
                //this.direction = new Vector3D(direction.X, 0, direction.Z);

                Vector3D currentDir = Vector3D.TransformNormal(this.direction, TheBlock.Top.CubeGrid.WorldMatrix);
                //currentDir = new Vector3D(currentDir.X, 0, currentDir.Z);


                if (point) PointRotorAtVector(TheBlock, desiredVec, currentDir/*theBlock.Top.WorldMatrix.Forward*/, multiplier);

                return AngleBetweenCos(currentDir, desiredVec, desiredVec.Length());
            }

            public double SetFromVec(Vector3D desiredVec, bool point = true)
            {
                return SetFromVec(desiredVec, 1, point);
            }

            // gets cos(angle between 2 vectors)
            // cos returns a number between 0 and 1
            // use Acos to get the angle
            //THIS COULD BE NECESSARY IN SOME FUTURE.....
            public double AngleBetweenCos(Vector3D a, Vector3D b)
            {
                double dot = Vector3D.Dot(a, b);
                double Length = a.Length() * b.Length();
                return dot / Length;
            }

            // gets cos(angle between 2 vectors)
            // cos returns a number between 0 and 1
            // use Acos to get the angle
            // doesn't calculate length because thats expensive
            public double AngleBetweenCos(Vector3D a, Vector3D b, double len_a_times_len_b)
            {
                double dot = Vector3D.Dot(a, b);
                return dot / len_a_times_len_b;
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


            public ShipController(IMyShipController theBlock/*, Program program*/) : base(theBlock)
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

            public BlockWrapper(T block)
            {
                TheBlock = block;
                Directions = GetDirections(block);
                CName = block.CustomName;
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
