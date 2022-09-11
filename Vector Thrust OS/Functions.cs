using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        void ParkVector(ref Vector3D requiredVec, float shipmass) {
            ShipController c = mainController ?? controlledControllers[0];
            double[] tdm = thrdirmultiplier;
            Vector3D zero_G_accel;
            Vector3D v1 = thrdiroverride ? Vector3D.Zero : requiredVec;
            Vector3D v2 = thrdiroverride ? Vector3D.Zero : requiredVec - shipVelocity;
            zero_G_accel = (c.TheBlock.WorldMatrix.Forward * tdm[0] + c.TheBlock.WorldMatrix.Up * tdm[1] + c.TheBlock.WorldMatrix.Right * tdm[2]) * zeroGAcceleration/* / 1.414f*/;
            requiredVec = dampeners ? zero_G_accel * shipmass + v1 : v2 + zero_G_accel;
        }

        string Separator(string title = "", int len = 58)
        {
            int tl = title.Length;
            len = (len - tl) / 2;
            string res = new string('-', len);
            return res + title + res;
        }

        void ThrustOnHandler() {
            double force = gravCutoff * myshipmass.PhysicalMass;
            double cutoffcruise = lowThrustCutCruiseOff * force;
            double cutoncruise = lowThrustCutCruiseOn * force;

            if (mvin != 0 || (dampchanged && dampeners) || (!cruise && sv > lowThrustCutOn) || (cruise && len > cutoncruise) || (trulyparked && wgv != 0 && sv != 0))
            {//this not longer causes problems if there are many small nacelles (SOLVED)
                thrustOn = true;
                trulyparked = false;
                //accelExponent_A = Accelerations[gear];
            }


            if (mvin == 0)
            {
                //accelExponent_A = 0;
                //if (wgv != 0 && sv == 0 && !parked)
                //{
                //if (thrustOn)
                //{
                //thrustontimer += Runtime.TimeSinceLastRun.TotalSeconds;
                /*Write($"{Spinner} STABILIZING {Spinner}\n");
                if (CanPrint()) screensb.Append($"{Spinner} STABILIZING {Spinner}\n");*/
                // It is so fast that is useless to try putting this here;
                //}
                //}
                //else thrustontimer = 0;
                bool trigger = /*thrustontimer > timeperframe * tpframes;*/wgv != 0 && sv == 0 && !parked;

                if ((wgv == 0 && ((!cruise && sv < lowThrustCutOff) || ((cruise || !dampeners) && len < cutoffcruise))) || !(!parked || !alreadyparked) || trigger)
                {
                    thrustOn = false;
                    if (trigger) trulyparked = true;
                } 
            }

            //Echo($"{thrustOn}/{trulyparked}/{sv}/{cruise}/{thrustontimer}/{parked}");
        }

        void GetAcceleration(/*double gravity, double exp = 0*/)
        {
            // look through boosts, applies acceleration of first one found (DEPRECATED)
            /*if (useBoosts && this.controlModule)
            {
                for (int i = 0; i < this.boosts.Length; i++)
                {
                    if (this.CMinputs.ContainsKey(this.boosts[i].button))
                    {
                        return this.boosts[i].accel * gravity * defaultAccel;
                    }
                }
            }*/
            

            //double accelexpaval = exp == 0 ? accelExponent_A : exp;
            double gravtdefac = gravLength * defaultAccel;
            double efectiveaccel = totaleffectivethrust / myshipmass.PhysicalMass * 1.4675;

            //getting max & gear accel
            gearaccel = efectiveaccel * Accelerations[gear] / 100;//Math.Pow(accelBase, accelExponent + Accelerations[gear]) * gravtdefac;
            maxaccel = efectiveaccel * Accelerations[Accelerations.Length -1] / 100;

            double gravaccel = accelBase/*.Pow(accelExponent) */* gravtdefac;
            bool cond = mvin == 0 && !cruise && dampeners && sv > thrustcutaccel && gearaccel > gravaccel;
            
            //double applied =/* wgv == 0 && */ cond ? maxaccel : accelBase.Pow(accelExponent) * gravtdefac;
            //Echo($"{accelExponent} / {accelExponent_A}");
            //none found or boosts not enabled, go for normal accel
            //return Math.Pow(accelBase, accelExponent + accelexpaval) * gravtdefac;
            //accel = Math.Pow(accelBase, accelExponent + accelexpaval) * gravtdefac;
            accel = mvin != 0 || cond ? gearaccel : gravaccel;

            //Echo($"{accel}");
        }

        bool CanPrint()
        {
            return pc % framesperprint == 0 || Runtime.UpdateFrequency != UpdateFrequency.Update1 || justCompiled;
        }

        void Printer()
        {
            
            //Write(screensb.ToString());

            if (CanPrint())
            {
                Echo(echosb.ToString());

                echosb.Clear();
                WH.Process();
                screensb.Clear();

                GenSpinner();

                string cstr = mainController != null ? mainController.TheBlock.CustomName : "DEAD";
                /*string pstr = parkavailable ? $" | Park: {(allowpark ? "On" : "Off")}" : "";
				if (ShowMetrics) screensb.Append($"{Spinner} {Runtime.LastRunTimeMs.Round(2)}ms {Spinner}").Append("\n");
				screensb.Append(progressbar);
				screensb.Append("\n").Append($"{Spinner} {trueaccel} {Spinner}");
				screensb.AppendLine($"\nCruise: {(cruise ? "On" : "Off")}{pstr}");
				if (normalThrusters.Count == 0) screensb.AppendLine($"Dampeners: {(dampeners ? "On" : "Off")}");*/
				if (ShowMetrics)
				{
                    StringBuilder metrics = new StringBuilder($"{Spinner} {Runtime.LastRunTimeMs.Round(2)}ms {Spinner}\n");
                    metrics.AppendLine($"AM: {(accel / gravLength).Round(2)}g");
                    metrics.AppendLine($"Active VectorThrusters: {vectorthrusters.Count}");
                    metrics.AppendLine($"Main/Ref Cont: {cstr}");
                    metrics.AppendLine($"ThrustOn: {thrustOn}");

                    echosb.Append(metrics);
                    screensb.Append(metrics);


                } else echosb.Append($"{Spinner} VtOS {Spinner}");

                echosb.AppendLine($"\n\n--- Main ---");
                echosb.AppendLine(" >Remaining: " + _RuntimeTracker.tremaining);
                echosb.AppendLine(" >Greedy: " + greedy);
                echosb.AppendLine($" >Angle Objective: {totalVTThrprecision.Round(1)}%");
                echosb.AppendLine($" >Main/Reference Controller: {cstr}");
                echosb.AppendLine($" >Parked: {parkedcompletely}/{unparkedcompletely}");
                if (isstation) echosb.AppendLine("CAN'T FLY A STATION, RUNNING WITH LESS RUNTIME.");
            }
        }

        bool SkipFrameHandler(string argument)
        {
            bool tagArg =
            argument.Contains(applyTagsArg) ||
            argument.Contains(cruiseArg) ||
            argument.Contains(removeTagsArg);

            bool handlers = false;
            if (!isstation)
            {
                MainChecker.Run();//RUNS VARIOUS PROCESSES SEPARATED BY A TIMER
                bool notrun = argument.Equals("") /*&& !cruise*/ && !dampchanged;
                if (notrun)
                {
                    handlers = PerformanceHandler();
                    handlers = ParkHandler() || handlers;
                    if (!cruise) handlers = VTThrHandler() || handlers;
                }
                else if (tagArg) MainTag(argument);
            }
            if (error)
            {
                ShutDown();
                return true;
            }
            else if (isstation) return true;
            else if (handlers)
            {
                if (CanPrint())
                {
                    echosb.AppendLine("Required Force: ---N");
                    echosb.AppendLine("Total Force: ---N\n");
                    echosb = _RuntimeTracker.Append(echosb);
                    echosb.AppendLine("--- Log ---");
                    echosb.Append(log);
                }
                _RuntimeTracker.AddInstructions();
            }
            return handlers;
        }
        void ShutDown()
        {
            if (wgv == 0)
            {
                vtthrusters.ForEach(tr => tr.Brake());
                log.AppendLine("0G Detected -> Braking Thrusters");
            }
            vtrotors.ForEach(rt => rt.Brake());
            log.AppendLine("Braking Rotors");

            if (WH != null) WH.BSOD();
            Echo(log.ToString());
            ChangeRuntime(4);
        }
        /*void GenerateProgressBar(string argument)
		{
			double percent = gearaccel / maxaccel;
			if (justCompiled || argument.Contains(gearArg) || gearIsPressed)
			{
				progressbar.Clear();
				progressbar.ProgressBar(percent, 30);
			}
			if (CanPrint()) trueaccel = $" ({(!thrustOn ? (percent * totaleffectivethrust).Round(2) : tthrust.Round(2))} {{m/s²}}) ";
		}*/

        public bool CheckRotor(IMyMotorStator rt)
        {
            return rt != null /*&& !rt.Closed && rt.IsWorking && rt.IsAlive()*/ && rt.Top != null && GridTerminalSystem.CanAccess(rt);
        }

        public void Print(params object[] args)
        {
            string separator = args[0].ToString();
            bool writes = false;
            if (separator.Contains("%sep%")) separator = separator.Replace("%sep%", "");
            else separator = " - ";
            StringBuilder result = new StringBuilder();

            foreach (object arg in args)
            {
                string arg_s = arg.ToString();
                if (arg_s.Contains("%scr%")) writes = true;
                else if (!arg_s.Contains("%sep%")) result.Append(arg_s).Append(separator);
            }

            log.AppendNR(result.ToString());
            if (writes) screensb.Append(result);
        }

        /*void Write(params object[] obj)
		{
			string sep = ", ";
			string init = obj[0].ToString();
			if (init.Contains("%sep%")) sep = init.Replace("%sep%", "");
			string result = string.Join(sep, obj) + "\n";
			if (this.surfaces.Count > 0)
			{
				foreach (IMyTextSurface surface in this.surfaces)
				{
					surface.WriteText(result, globalAppend);
					surface.ContentType = ContentType.TEXT_AND_IMAGE;
					surface.Alignment = TextAlignment.CENTER;
				}
			}
			else if (!globalAppend)
			{
				string err = "\nNo text surfaces available";
				string errs = " because the ship is a station.";
				if (!justCompiled)
				{
					log.AppendNR(err);
					if (isstation)
					{
						Echo(err + errs);
						log.AppendNR(errs);
					}
				}
			}
			globalAppend = true;
		}*/

        void LND<T>(ref List<T> obj)
        {
            obj = obj.Distinct().ToList();
        }
        public bool FilterThis(IMyTerminalBlock block)
        {
            return block.CubeGrid == Me.CubeGrid;
        }

        public void StabilizeRotors(bool rotorlock = true)
        {
            //if (vtthrusters.Empty() && vtrotors.Empty()) return;
            //vtthrusters.ForEach(x => { x.Enabled = !rotorlock; if (rotorlock) x.ThrustOverridePercentage = 0; });
            foreach (IMyMotorStator r in vtrotors) {
                if (rotorlock) r.TargetVelocityRPM = 0;
                if ((rotorlock && r.TargetVelocityRPM == 0) || !rotorlock) r.Enabled = !rotorlock;
                r.RotorLock = rotorlock;
            }

            //vtrotors.ForEach(x => { if ((rotorlock && x.TargetVelocityRPM == 0) || !rotorlock) x.Enabled = !rotorlock; if (rotorlock) x.TargetVelocityRPM = 0; x.RotorLock = rotorlock; });
        }

        public void ShutOffThrusters(bool rotorlock = true)
        {
            foreach (IMyThrust t in vtthrusters)
            {
                t.Enabled = !rotorlock;
                if (rotorlock) t.ThrustOverridePercentage = 0;
            }
            //if (vtthrusters.Empty() && vtrotors.Empty()) return;
            //vtthrusters.ForEach(x => { x.Enabled = !rotorlock; if (rotorlock) x.ThrustOverridePercentage = 0; });
            //vtrotors.ForEach(x => { if ((rotorlock && x.TargetVelocityRPM == 0) || !rotorlock) x.Enabled = !rotorlock; if (rotorlock) x.TargetVelocityRPM = 0; x.RotorLock = rotorlock; });
        }

        ShipController FindACockpit()
        {
            foreach (ShipController cont in controlledControllers)
            {
                if (cont.TheBlock.IsWorking/* && GridTerminalSystem.CanAccess(cont.TheBlock)*/)
                {
                    return cont;
                }
            }

            return null;
        }

        void OneRunMainChecker(bool run = true)
        {
            ResetVTHandlers();
            check = true;
            if (run) MainChecker.Run();
        }

        void MainTag(string argument)
        {
            //tags and getting blocks
            TagAll = argument.Contains(applyTagsAllArg);
            this.applyTags = argument.Contains(applyTagsArg) || TagAll;
            this.greedy = (!this.applyTags && this.greedy);
            if (this.applyTags)
            {
                AddTag(Me);
            }
            else if (argument.Contains(removeTagsArg)) ManageTag(true, false); // New remove tags.

            OneRunMainChecker();

            TagAll = false;
            this.applyTags = false;
        }

        void ManageTag(bool force = false, bool logthis = true)
        {
            tag = tagSurround[0] + myName + tagSurround[1];
            bool cond1 = oldTag.Length > 0;
            bool cond2 = !tag.Equals(oldTag) && Me.CustomName.Contains(oldTag);
            bool cond3 = greedy && Me.CustomName.Contains(oldTag);

            if (cond1 && (cond2 || cond3 || force))
            {
                if (logthis) log.AppendNR(" -Cleaning Tags To Prevent Future Errors, just in case\n");
                else log.AppendNR(" -Removing Tags\n");
                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);
                foreach (IMyTerminalBlock block in blocks) RemoveTag(block);
            }
            this.greedy = !HasTag(Me);
            oldTag = tag;
        }


        bool HasTag(IMyTerminalBlock block)
        {
            return block.CustomName.Contains(tag);
        }

        void AddTag(IMyTerminalBlock block)
        {
            string name = block.CustomName;
            if (!name.Contains(tag)) block.CustomName = tag + " " + name;
        }

        void RemoveTag(IMyTerminalBlock block)
        {
            block.CustomName = tag == oldTag ? block.CustomName.Replace(tag, "").Trim() : block.CustomName.Replace(oldTag, "").Trim();
        }

        void Init()
        {
            log.AppendLine("Init() Start");
            Echo("Init() Start");
            Config();
            Echo("Config() End");
            ManageTag();
            Echo("ManageTag() End");
            InitControllers();
            Echo("InitControllers() End");

            check = true;
            if (mainController != null)
            {
                myshipmass = mainController.TheBlock.CalculateShipMass();
                oldMass = myshipmass.BaseMass;
            }
            Echo("Checking Mass End");
            OneRunMainChecker();
            Echo("OneRunMainChecker() End");
            log.AppendLine("Init " + (error ? "Failed" : "Completed Sucessfully"));
        }

        void InitControllers() //New GetControllers(), only for using in init() purpose 
        {
            bool greedy = this.greedy || this.applyTags;

            List<IMyShipController> blocks = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks);

            List<ShipController> conts = new List<ShipController>();
            foreach (IMyShipController imy in blocks)
            {
                controllerblocks.Add(imy);
                conts.Add(new ShipController(imy));
            }

            controllers = conts;

            StringBuilder reason = new StringBuilder();
            foreach (ShipController s in controllers)
            {
                bool canAdd = true;
                StringBuilder currreason = new StringBuilder(s.TheBlock.CustomName + "\n");
                if (!s.TheBlock.ShowInTerminal && ignoreHiddenBlocks)
                {
                    currreason.AppendLine("  ShowInTerminal not set\n");
                    canAdd = false;
                }
                if (!s.TheBlock.CanControlShip)
                {
                    currreason.AppendLine("  CanControlShip not set\n");
                    canAdd = false;
                }
                if (!s.TheBlock.ControlThrusters)
                {
                    currreason.AppendLine("  Can't ControlThrusters\n");
                    canAdd = false;
                }
                /*if (s.theBlock.IsMainCockpit)
				{ // I thiink this could make problems in the future
					mainController = s;
				}*/
                if (!(greedy || HasTag(s.TheBlock)))
                {
                    currreason.AppendLine("  Doesn't match my tag\n");
                    canAdd = false;
                }

                if (canAdd)
                {
                    AddSurfaceProvider(s.TheBlock);
                    s.Dampener = s.TheBlock.DampenersOverride;
                    controlledControllers.Add(s);
                    ccontrollerblocks.Add(s.TheBlock);

                    if (this.applyTags)
                    {
                        AddTag(s.TheBlock);
                    }
                }
                else
                {
                    reason.Append(currreason);
                }
            }
            if (blocks.Count == 0)
            {
                reason.AppendLine("No Controller Found.\nEither for missing tag, not working or removed.");
            }

            if (controlledControllers.Count == 0)
            {
                log.AppendNR("ERROR: no usable ship controller found. Reason: \n");
                log.AppendNR(reason.ToString());
                ManageTag(true);
                error = true;
                return;
            }

            else if (controlledControllers.Count > 0)
            {
                foreach (ShipController s in controlledControllers)
                {
                    if (s.TheBlock.IsUnderControl)
                    {
                        mainController = s;
                        break;
                    }
                }
                if (mainController == null)
                {
                    mainController = controlledControllers[0];
                }
            }
            return;
        }

        // true: only main cockpit can be used even if there is no one in the main cockpit
        // false: any cockpits can be used, but if there is someone in the main cockpit, it will only obey the main cockpit
        // no main cockpit: any cockpits can be used
        bool OnlyMain()
        {
            return mainController != null && (mainController.TheBlock.IsUnderControl || onlyMainCockpit);
        }

        bool doneunstop = false;
        //bool triedtostop = false;
        double temp1 = 0;
        //int temp2 = 0;



        bool VTThrHandler()
        {
            bool nograv = wgv == 0;
            //bool preventer = trulyparked && !nograv && !parked && sv != 0;

            bool unparking = !parked && alreadyparked;
            bool partiallyparked = parked && alreadyparked;
            bool standby = (nograv || partiallyparked) && /*totalVTThrprecision.Round(1) == 100*/temp1 > 0.5 && setTOV && !thrustOn && mvin == 0;

            if (!thrustOn && totalVTThrprecision.Round(1) == 100 && temp1 <= 0.5) temp1 += Runtime.TimeSinceLastRun.TotalSeconds;
            else if (thrustOn) temp1 = 0;

            //bool triedtostop = vtrotors.Any(x => !x.Enabled || x.RotorLock);
            //if (CanPrint()) screensb.AppendLine($"du:{doneunstop} pc:{parkedcompletely} st:{standby}\ntov:{setTOV} thron:{thrustOn} p:{totalVTThrprecision.Round(1) == 100}\nt1:{temp1} t2:{temp2}");
            //if (CanPrint()) screensb.AppendLine($"t: {temp1}");

            if (standby || parkedcompletely)
            {
                if (CanPrint()) echosb.AppendLine("\nEverything stopped, performance mode.\n");

                //bool cond1 = vtthrusters.All(x => x.Enabled == false && x.ThrustOverridePercentage == 0);
                bool cond2 = vtrotors.All(x => !x.Enabled && x.RotorLock) && vtthrusters.All(x => !x.Enabled && x.ThrustOverridePercentage == 0);

                //if (CanPrint()) screensb.AppendLine($"cond2: {cond2}");

                rotorsstopped = rotorsstopped || /*cond1 && */cond2;

                if (!rotorsstopped/* && temp1 > 0*/)
                {
                    StabilizeRotors();
                    ShutOffThrusters();
                    doneunstop = false;
                    ////_RuntimeTracker.RegisterAction("VTHandlerTrue1");
                }
                else if (cond2 && totalVTThrprecision.Round(1) == 100) {
                    StabilizeRotors(false);
                    
                    ////_RuntimeTracker.RegisterAction("VTHandlerTrue2");
                } //Unlocking rotors for free use

                //temp1++;
                return true;
            }
            else if (((rotorsstopped && setTOV) || unparking) && !doneunstop)  /*|| preventer*/ // IT NEEDS TO BE UNPARKING INSTEAD OF TOTALLY UNPARKED
            {
                //temp2++;
                setTOV = rotorsstopped/* = trulyparked */= false;
                ShutOffThrusters(false);
                StabilizeRotors(false); //Just in case
                //if (preventer) thrustontimer = 0;

                foreach (VectorThrust n in vectorthrusters)
                    n.ActiveList(Override: true);
                doneunstop = vtthrusters.All(x => x.Enabled);
                //_RuntimeTracker.RegisterAction("VTHandlerFalse");
            }
            return rotorsstopped;
        }
        bool PerformanceHandler()
        {
            if (SkipFrames > 0 && CanPrint())
            {
                echosb.AppendLine($"--SkipFrame[{ SkipFrames}]--");
                echosb.AppendLine($" >Skipped: {frame}");
                echosb.AppendLine($" >Remaining: {SkipFrames - frame}");
            }
            if (!justCompiled && SkipFrames > 0 && SkipFrames > frame)
            {
                frame++;
                ////_RuntimeTracker.RegisterAction("SkFTrue");
                return true;
            }
            else if (SkipFrames > 0 && frame >= SkipFrames) frame = 0;
            ////_RuntimeTracker.RegisterAction("SkFFalse");
            return false;
        }

        bool forceunpark = false;


        bool ParkHandler()
        {
            parkavailable = !connectors.Empty() || !landinggears.Empty();
            //if (trulyparked && !parked && CanPrint()) screensb.AppendLine($"- (NOT) PARKED -");
            if (!parkavailable && !forceunpark) return false;

            bool changedpark = false;
            if (unparkedcompletely)
            {
                parkedwithcn = connectors.Any(x => x.Status == MyShipConnectorStatus.Connected);
                parked = (landinggears.Any(x => x.IsLocked) || parkedwithcn) && (/*(wgv == 0 && allowpark) || (wgv != 0 && allowpark*/ allowpark || (trulyparked && forceparkifstatic));
                ////_RuntimeTracker.RegisterAction("ParkCheck");
            }
            else
            { //Modifying
                bool newpark = (landinggears.Any(x => x.IsLocked) || connectors.Any(x => x.Status == MyShipConnectorStatus.Connected)) && (allowpark || (trulyparked && forceparkifstatic && sv == 0));
                changedpark = newpark != parked;
                parked = newpark;
                ////_RuntimeTracker.RegisterAction("UnParkCheck");
            }
            unparkedcompletely = !parked && !alreadyparked;
            if (unparkedcompletely) return false;

            bool setvector = parked && alreadyparked && setTOV;
            bool gotvector = totalVTThrprecision.Round(1) == 100 && temp1 > 0.5;
            parkedcompletely = setvector && gotvector;
            //bool rarepark = !parked && alreadyparked && setTOV && gotvector;
            //if (changedpark) screensb.AppendLine($"AAAAAAAAAAAAAAAAAAAA");

            bool pendingrotation = setvector && !gotvector;
            bool parking = parked && !alreadyparked;
            bool unparking = !parked && alreadyparked;

            /*if (CanPrint()) { 
                screensb.AppendLine($"E: {changedpark}/{parked}/{trulyparked}"); 
                screensb.AppendLine($"E: {alreadyparked}{BlockManager.Doneloop}"); 
            }*/

            if (parking || (unparking && BlockManager.Doneloop) || changedpark) { 
                ResetParkingSeq();
                //temp1 = 0;
                ////_RuntimeTracker.RegisterAction("ResetParkingSeq");
            }
            if (parkedcompletely || (unparking && !BlockManager.Doneloop))
            {
                //string l = parkedcompletely ? "ParkLoop" : "ParkParking";

                ////_RuntimeTracker.RegisterAction(l);
                //if (rarepark) screensb.AppendLine("RARE");
                /*if (CanPrint())
                {
                    if (parkedcompletely && BlockManager.Doneloop) screensb.AppendLine("- PARKED - ");
                    else if (parkedcompletely && !BlockManager.Doneloop) screensb.Append($"{Spinner} ASSIGNING {Spinner}\n");
                    else screensb.Append($"{Spinner} UNPARKING {Spinner}\n");
                }*/
                BlockManager.Run();
            }
            //if (parking && CanPrint()) screensb.Append($"{Spinner} PARKING {Spinner}\n");


            if (unparkedcompletely) forceunpark = false;
            return parkedcompletely;
        }

        string Spinner = "";

        void GenSpinner()
        {
            switch (pc / 10 % 4)
            {
                case 0:
                    Spinner = "|";
                    break;
                case 1:
                    Spinner = "\\";
                    break;
                case 2:
                    Spinner = "-";
                    break;
                case 3:
                    Spinner = "/";
                    break;
            }
        }

        bool AddSurfaceProvider(IMyTerminalBlock block)
        {
            if (!(block is IMyTextSurfaceProvider)) return false;
            IMyTextSurfaceProvider provider = (IMyTextSurfaceProvider)block;
            bool retval = true;
            if (block.CustomData.Length == 0)
            {
                return false;
            }

            bool[] to_add = new bool[provider.SurfaceCount];
            for (int i = 0; i < to_add.Length; i++)
            {
                to_add[i] = false;
            }
            int begin_search = 0;

            while (begin_search >= 0)
            {
                string data = block.CustomData;
                int start = data.IndexOf(textSurfaceKeyword, begin_search);

                if (start < 0)
                {
                    retval = begin_search != 0;
                    break;
                }
                int end = data.IndexOf("\n", start);
                begin_search = end;

                string display;
                if (end < 0)
                {
                    display = data.Substring(start + textSurfaceKeyword.Length);
                }
                else
                {
                    display = data.Substring(start + textSurfaceKeyword.Length, end - (start + textSurfaceKeyword.Length));
                }

                int display_num;
                if (Int32.TryParse(display, out display_num))
                {
                    if (display_num >= 0 && display_num < provider.SurfaceCount)
                    {
                        // it worked, add the surface
                        to_add[display_num] = true;

                    }
                    else
                    {
                        // range check failed
                        string err_str;
                        if (end < 0)
                        {
                            err_str = data.Substring(start);
                        }
                        else
                        {
                            err_str = data.Substring(start, end - (start));
                        }
                        surfaceProviderErrorStr.Append($"\nDisplay number out of range: {display_num}\nshould be: 0 <= num < {provider.SurfaceCount}\non line: ({err_str})\nin block: {block.CustomName}\n");
                    }

                }
                else
                {
                    //didn't parse
                    string err_str;
                    if (end < 0)
                    {
                        err_str = data.Substring(start);
                    }
                    else
                    {
                        err_str = data.Substring(start, end - (start));
                    }
                    surfaceProviderErrorStr.Append($"\nDisplay number invalid: {display}\non line: ({err_str})\nin block: {block.CustomName}\n");
                }
            }

            for (int i = 0; i < to_add.Length; i++)
            {
                if (to_add[i] && !this.surfaces.Contains(provider.GetSurface(i)))
                {
                    this.surfaces.Add(provider.GetSurface(i));
                }
                else if (!to_add[i])
                {
                    RemoveSurface(provider.GetSurface(i));
                }
            }
            return retval;
        }
        bool RemoveSurfaceProvider(IMyTerminalBlock block)
        {
            if (!(block is IMyTextSurfaceProvider)) return false;
            IMyTextSurfaceProvider provider = (IMyTextSurfaceProvider)block;

            for (int i = 0; i < provider.SurfaceCount; i++)
            {
                if (surfaces.Contains(provider.GetSurface(i)))
                {
                    RemoveSurface(provider.GetSurface(i));
                }
            }
            return true;
        }

        void RemoveSurface(IMyTextSurface surface)
        {
            if (this.surfaces.Contains(surface))
            {
                //need to check this, because otherwise it will reset panels
                //we aren't controlling
                this.surfaces.Remove(surface);
                surface.ContentType = ContentType.NONE;
                surface.WriteText("", false);
            }
        }

        bool cruisebyarg = false;
        Vector3D GetMovementInput(string arg, bool perf = false)
        {
            Vector3D moveVec = Vector3D.Zero;

            if (controlModule)
            {
                // setup control module
                Dictionary<string, object> inputs = new Dictionary<string, object>();
                try
                {
                    this.CMinputs = Me.GetValue<Dictionary<string, object>>("ControlModule.Inputs");
                    Me.SetValue<string>("ControlModule.AddInput", "all");
                    Me.SetValue<bool>("ControlModule.RunOnInput", true);
                    Me.SetValue<int>("ControlModule.InputState", 1);
                    Me.SetValue<float>("ControlModule.RepeatDelay", 0.016f);
                }
                catch
                {
                    controlModule = false;
                }
            }

            if (controlModule)
            {
                // non-movement controls
                if (!dampenersIsPressed && this.CMinputs.ContainsKey(dampenersButton))
                {//inertia dampener key
                    dampeners = !dampeners;//toggle
                    dampenersIsPressed = true;
                }
                else if (dampenersIsPressed && !this.CMinputs.ContainsKey(dampenersButton))
                {
                    dampenersIsPressed = false;
                }

                if (!cruiseIsPressed && this.CMinputs.ContainsKey(cruiseButton))
                {//cruise key
                    cruise = !cruise;//toggle
                    cruiseIsPressed = true;
                }
                else if (cruiseIsPressed && !this.CMinputs.ContainsKey(cruiseButton))
                {
                    cruiseIsPressed = false;
                }

                if (!gearIsPressed && this.CMinputs.ContainsKey(gearButton))
                {//throttle up
                    if (gear == Accelerations.Length - 1) gear = 0;
                    else gear++;
                    gearIsPressed = true;
                }
                else if (gearIsPressed && !this.CMinputs.ContainsKey(gearButton))
                { //increase target acceleration
                    gearIsPressed = false;
                }

                if (!allowparkIsPressed && this.CMinputs.ContainsKey(allowparkButton))
                {//throttle down
                    allowpark = !allowpark;
                    allowparkIsPressed = true;
                }
                else if (allowparkIsPressed && !this.CMinputs.ContainsKey(allowparkButton))
                { //increase target acceleration
                    allowparkIsPressed = false;
                }
            }

            bool changeDampeners = false;

            if (arg.Length > 0)
            {
                if (arg.Contains(dampenersArg))
                {
                    dampeners = !dampeners;
                    changeDampeners = true;
                }
                else if (arg.Contains(cruiseArg))
                {
                    cruise = !cruise;
                    cruisebyarg = cruise;
                }
                /*else if (arg.Contains(raiseAccelArg))
                {
                    accelExponent++;
                }
                else if (arg.Contains(lowerAccelArg))
                {
                    accelExponent--;
                }
                else if (arg.Contains(resetAccelArg))
                {
                    accelExponent = 0;
                }*/
                else if (arg.Contains(gearArg))
                {
                    if (gear == Accelerations.Length - 1) gear = 0;
                    else gear++;
                }
                else if (arg.Contains("park"))
                {
                    allowpark = !allowpark;
                    forceunpark = true;
                }
            }


            // dampeners (if there are any normal thrusters, the dampeners control works)
            if (normalThrusters.Count != 0)
            {

                if (OnlyMain())
                {

                    if (changeDampeners)
                    {
                        mainController.TheBlock.DampenersOverride = dampeners;
                    }
                    else
                    {
                        dampeners = mainController.TheBlock.DampenersOverride;
                    }
                }
                else
                {

                    if (changeDampeners)
                    {
                        // make all conform
                        foreach (ShipController cont in controlledControllers)
                        {
                            cont.SetDampener(dampeners);
                        }
                    }
                    else
                    {

                        // check if any are different to us
                        bool any_different = false;
                        foreach (ShipController cont in controlledControllers)
                        {
                            if (cont.TheBlock.DampenersOverride != dampeners)
                            {
                                any_different = true;
                                dampeners = cont.TheBlock.DampenersOverride;
                                break;
                            }
                        }

                        if (any_different)
                        {
                            // update all others to new value too
                            foreach (ShipController cont in controlledControllers)
                            {
                                cont.SetDampener(dampeners);
                            }
                        }
                    }
                }
            }


            // movement controls
            if (perf) return moveVec; //Vector3D.Zero

            if (OnlyMain())
            {
                moveVec = mainController.TheBlock.GetWorldMoveIndicator();
            }
            else
            {
                foreach (ShipController cont in controlledControllers)
                {
                    if (cont.TheBlock.IsUnderControl)
                    {
                        moveVec += cont.TheBlock.GetWorldMoveIndicator();
                    }
                }
            }

            return moveVec;
        }

        void CheckWeight()
        {
            ShipController cont = FindACockpit();

            if (cont == null)
            {
                log.AppendNR("  -No cockpit registered, checking mainController\n");
                if (!GridTerminalSystem.CanAccess(mainController.TheBlock))
                {
                    mainController = null;
                    foreach (ShipController c in controlledControllers)
                    {
                        if (GridTerminalSystem.CanAccess(c.TheBlock))
                        {
                            mainController = c;
                            break;
                        }
                    }
                }
                if (mainController == null)
                {
                    error = true;
                    log.AppendNR("ERROR, ANY CONTROLLERS FOUND - SHUTTING DOWN");
                    ManageTag(true);
                    return;
                }
            }
            else if (!applyTags)
            {
                myshipmass = cont.TheBlock.CalculateShipMass();
                float bm = myshipmass.BaseMass;

                if (bm < 0.001f)
                {
                    log.AppendNR("  -Can't fly a Station\n");
                    isstation = true;
                    ChangeRuntime(2);
                    return;
                }
                else if (isstation)
                {
                    isstation = false;
                    ChangeRuntime(5);
                }

                if (this.oldMass == bm) return; //modifying variables here may cause to the handler to restart every single time

                this.oldMass = bm; //else:
            }
            OneRunMainChecker(false);
            ////_RuntimeTracker.RegisterAction("CheckWeight");
            //if (!justCompiled) GenerateProgressBar(true);
        }

        void EndBM(bool scanned)
        {
            if (scanned && parked)
            {
                BlockManager.Doneloop = true;
                if (PerformanceWhilePark && wgv == 0 && Runtime.UpdateFrequency != UpdateFrequency.Update100) ChangeRuntime(2);
                else if ((!PerformanceWhilePark || wgv > 0) && Runtime.UpdateFrequency != UpdateFrequency.Update10) ChangeRuntime(1);
            }
            else if (!parked)
            {
                parkedwithcn = alreadyparked = false;
            }
        }

        void ChangeRuntime(int n = 0)
        {
            switch (n)
            {
                case 0: Runtime.UpdateFrequency = UpdateFrequency.Update1; /*updatespersecond = 60;*/ /*timeperframe = 1.0 / 60.0; */break;
                case 1: Runtime.UpdateFrequency = UpdateFrequency.Update10; /*updatespersecond = 6;*/ /*timeperframe = 1.0 / 6.0; */break;
                case 2: Runtime.UpdateFrequency = UpdateFrequency.Update100; /*updatespersecond = 1;*//* timeperframe = 6; */break;
                case 3: Runtime.UpdateFrequency = UpdateFrequency.Once; break;
                case 4: Runtime.UpdateFrequency = UpdateFrequency.None; break;
                case 5: Runtime.UpdateFrequency = update_frequency; /*updatespersecond = dupdatespersecond;*/ /*timeperframe = dtimeperframe;*/ break;
            };
        }
    }
}
