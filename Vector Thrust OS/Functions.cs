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
        void ParkVector(ref Vector3D requiredVec, float shipmass)
        {
            if (thrustOn) return;
            ShipController c = mainController ?? controlledControllers[0];
            List<double> tdm = thrdirmultiplier;
            Vector3D zero_G_accel;
            Vector3D v1 = thrdiroverride ? Vector3D.Zero : requiredVec;
            Vector3D v2 = thrdiroverride ? Vector3D.Zero : requiredVec - shipVelocity;
            zero_G_accel = (c.TheBlock.WorldMatrix.Forward * tdm[0] + c.TheBlock.WorldMatrix.Up * tdm[1] + c.TheBlock.WorldMatrix.Right * tdm[2]) * zeroGAcceleration/* / 1.414f*/;
            requiredVec = dampeners ? zero_G_accel * shipmass + v1 : v2 + zero_G_accel;
            setTOV = true;
        }

        /*string Separator(string title = "", int len = 58)
        {
            int tl = title.Length;
            len = (len - tl) / 2;
            string res = new string('-', len);
            return res + title + res;
        }*/

        double force = 0;

        void ThrustOnHandler()
        {
            force = gravCutoff * myshipmass.PhysicalMass;
            double cutoffcruise = lowThrustCutCruiseOff * force;
            double cutoncruise = lowThrustCutCruiseOn * force;

            if (mvin != 0 || (dampchanged && dampeners) || (!cruise && sv > lowThrustCutOn) || (cruise && len > cutoncruise) || (trulyparked && wgv != 0 && sv != 0))
            {
                thrustOn = true;
                trulyparked = false;
            }

            if (mvin == 0)
            {
                bool trigger = wgv != 0 && sv == 0 && !parked;

                if ((wgv == 0 && ((!cruise && sv < lowThrustCutOff) || ((cruise || !dampeners) && len < cutoffcruise))) || !(!parked || !alreadyparked) || trigger)
                {
                    thrustOn = false;
                    if (trigger) trulyparked = true;
                }
            }
        }

        void GetAcceleration()
        {
            //TODO MAKE MULTIPLIER BY NUMBER OF THRUSTERS

            double gravtdefac = gravLength * defaultAccel;
            double efectiveaccel = totaleffectivethrust/ myshipmass.BaseMass; //1.4675
            rawgearaccel /= myshipmass.PhysicalMass;

            //getting max & gear accel
            gearaccel = efectiveaccel * Accelerations[gear] / 100;
            rawgearaccel *= Accelerations[gear] / 100;
            maxaccel = efectiveaccel * Accelerations[Accelerations.Count - 1] / 100;

            double gravaccel = accelBase * gravtdefac;
            bool cond = mvin == 0 && !cruise && dampeners && sv > velprecisionmode && gearaccel > gravaccel;
            accel = mvin != 0 || cond ? gearaccel : gravaccel;

            accel_aux = !thrustOn || almostbraked ? (float)rawgearaccel.Round(2) : (float)((shipVelocity - lastvelocity) * updatespersecond).Length();
        }

        //bool CanPrint() => pc % framesperprint == 0 || Runtime.UpdateFrequency != UpdateFrequency.Update1 || justCompiled;

        void Printer()
        {
            if (!tracker.CanPrint) return;

            Echo(echosb.ToString());
            echosb.Clear();
            WH.Process();
            screensb.Clear();

            string cstr = mainController != null ? mainController.TheBlock.CustomName : "DEAD";

            if (ShowMetrics)
            {
                string rt = $" {tracker.LastRuntime.Round(3)}   {tracker.AverageRuntime.Round(3)}";
                echosb.AppendLine(rt);
                screensb.AppendLine(rt);
            }
            echosb.AppendLine("VT OS\n221132\n");

            if (greedy) echosb.AppendLine("WARNING, TAGS ARE NOT APPLIED\nAPPLY THEM WITH \"applytags\"\n");
            echosb.AppendLine($" > Thrusters Total Precision: {totalVTThrprecision.Round(1)}%");
            echosb.AppendLine($" > Main/Ref Controller:\n  {cstr}");
            echosb.AppendLine($" > Runtime (MS):\n  {Runtime.LastRunTimeMs.Round(3)} / Avg: {tracker.AverageRuntime.Round(3)} / Max: {tracker.MaxRuntime.Round(3)}");
            if (SkipFrames > 0) echosb.AppendLine($" > Skipping {SkipFrames} Frames");
            echosb.Append(surfaceProviderErrorStr);

            if (isstation) echosb.AppendLine("CAN'T FLY A STATION, RUNNING WITH LESS RUNTIME.");

            if (ShowMetrics)
            {
                StringBuilder metrics = new StringBuilder($"\n > Metrics:\n  Total VectorThrusters: {vectorthrusters.Count}\n");
                metrics.AppendLine($"  Main/Ref Cont: {cstr}");
                metrics.AppendLine($"  Parked: {parkedcompletely}/{unparkedcompletely}");
                metrics.AppendLine($"  ThrustOn: {thrustOn}");

                echosb.Append(metrics);
                screensb.Append(metrics);

                echosb.Append("\n > Log:\n").Append(log);
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
                //Print($"A {Runtime.UpdateFrequency}");

                //FOR SOME STUPID REASON PARKHANDLER IS SETTING UPDATE10 ALL THE TIME

                //bool notrun = argument.Equals("") /*&& !cruise && !dampchanged*/;
                //if (notrun)
                //{
                    //Print($"1 {Runtime.UpdateFrequency}");
                    handlers = PerformanceHandler();
                    //Print($"2 {Runtime.UpdateFrequency}");
                    handlers = ParkHandler() || handlers;
                    //Print($"3 {Runtime.UpdateFrequency}");
                    if (!cruise) handlers = VTThrHandler() || handlers;

                    if (check && parkedcompletely && BlockManager.Doneloop && Runtime.UpdateFrequency != UpdateFrequency.Update1)
                    {
                        ChangeRuntime();
                        //Print($"O {Runtime.UpdateFrequency}");
                    }
                    else if (!check && parkedcompletely && BlockManager.Doneloop && Runtime.UpdateFrequency == UpdateFrequency.Update1)
                    {
                        ChangeRuntime(PerformanceWhilePark && wgv == 0 ? 2 : 1);
                        //Print($"E {Runtime.UpdateFrequency}");

                    }
                    //Print($"4 {Runtime.UpdateFrequency}");
                //}
                /*else*/ if (tagArg) MainTag(argument);
            }
            if (error)
            {
                ShutDown();
                return true;
            }
            else if (isstation) {
                rotorsstopped = rotorsstopped || vtrotors.All(x => x.TargetVelocityRPM == 0) && vtthrusters.All(x => !x.Enabled && x.ThrustOverridePercentage == 0);
                ShutOffVTS();
                return true;
            }
            /*else if (handlers)
            {
                if (CanPrint())
                {
                    echosb.AppendLine("Required Force: ---N");
                    echosb.AppendLine("Total Force: ---N\n");
                    echosb = _RuntimeTracker.Append(echosb);

                    if (ShowMetrics)
                    {
                        echosb.AppendLine("--- Log ---");
                        echosb.Append(log);
                    }
                }
                _RuntimeTracker.AddInstructions();
            }*/
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

        public bool CheckRotor(IMyMotorStator rt)
        {
            return rt != null && rt.Top != null && GridTerminalSystem.CanAccess(rt);
        }

        public void Print(string sp, bool e = true, params object[] args)
        {
            if (!tracker.CanPrint) return;
            StringBuilder result = args.Length != 0 ? new StringBuilder().Append(string.Join(sp, args)) : new StringBuilder(sp);
            screensb.Append(result + "\n");
            if (e) echosb.Append(result + "\n");
        }

        void LND<T>(ref List<T> obj)
        {
            obj = obj.Distinct().ToList();
        }
        public bool FilterThis(IMyTerminalBlock block)
        {
            return block.CubeGrid == Me.CubeGrid;
        }

        void ShutOffVTS() {
            foreach (VectorThrust n in vectorthrusters)
            {
                foreach (Thruster t in n.thrusters)
                {
                    t.TheBlock.Brake();
                    t.TheBlock.Enabled = false;
                }
                n.rotor.TheBlock.Brake();
            }
        }

        ShipController FindACockpit()
        {
            if (mainController.TheBlock.IsWorking) return mainController;

            foreach (ShipController cont in controlledControllers)
            {
                if (cont.TheBlock.IsWorking)
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
                conts.Add(new ShipController(imy, this));
            }

            controllers = conts;

            StringBuilder reason = new StringBuilder();
            foreach (ShipController s in controllers)
            {
                bool canAdd = true;
                StringBuilder currreason = new StringBuilder(s.TheBlock.CustomName + "\n");
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
            return onlyMainCockpit && mainController != null && mainController.TheBlock.IsUnderControl;
        }

        //bool doneunstop = false;
        double tgotTOV = 0;


        bool VTThrHandler()
        {
            bool nograv = wgv == 0;
            bool unparking = !parked && alreadyparked;
            bool partiallyparked = parked && alreadyparked;
            bool standby = (nograv || partiallyparked) && tgotTOV > 0.25 && setTOV && !thrustOn && mvin == 0 && !dampchanged;

            if (!thrustOn && totalVTThrprecision.Round(1) == 100 && tgotTOV <= 0.25) tgotTOV += Runtime.TimeSinceLastRun.TotalSeconds;
            else if (thrustOn) tgotTOV = 0;

            if (standby || parkedcompletely)
            {
                if (tracker.CanPrint) echosb.AppendLine("\nEverything stopped, performance mode.\n");
                rotorsstopped = rotorsstopped || vtrotors.All(x => x.TargetVelocityRPM == 0) && vtthrusters.All(x => !x.Enabled && x.ThrustOverridePercentage == 0);
                //doneunstop = true;

                if (!rotorsstopped) ShutOffVTS();
                return true;
            }
            else if (((rotorsstopped && setTOV) || unparking/* || dampchanged*/)/* && doneunstop*/) // IT NEEDS TO BE UNPARKING INSTEAD OF TOTALLY UNPARKED
            {
                setTOV = rotorsstopped = false;

                foreach (VectorThrust n in vectorthrusters)
                    n.ActiveList(Override: true);
                //doneunstop = !vtthrusters.All(x => x.Enabled);
            }
            return rotorsstopped;
        }
        bool PerformanceHandler()
        {
            if (SkipFrames > 0 && tracker.CanPrint)
            {
                echosb.AppendLine($"--SkipFrame[{ SkipFrames}]--");
                echosb.AppendLine($" >Skipped: {frame}");
                echosb.AppendLine($" >Remaining: {SkipFrames - frame}");
            }
            if (!justCompiled && SkipFrames > 0 && SkipFrames > frame)
            {
                frame++;
                return true;
            }
            else if (SkipFrames > 0 && frame >= SkipFrames) frame = 0;
            return false;
        }

        bool forceunpark = false;


        bool ParkHandler()
        {
            parkavailable = !connectors.Empty() || !landinggears.Empty();
            if (!parkavailable && !forceunpark) return false;

            bool changedpark = false;
            if (unparkedcompletely)
            {
                parkedwithcn = connectors.Any(x => x.Status == MyShipConnectorStatus.Connected);
                parked = (landinggears.Any(x => x.IsLocked) || parkedwithcn) && (allowpark || (trulyparked && forceparkifstatic));
            }
            else
            { //Modifying
                bool newpark = (landinggears.Any(x => x.IsLocked) || connectors.Any(x => x.Status == MyShipConnectorStatus.Connected)) && (allowpark || (trulyparked && forceparkifstatic && sv == 0)) && !dampchanged;
                changedpark = newpark != parked;
                parked = newpark;
            }
            unparkedcompletely = !parked && !alreadyparked;
            if (unparkedcompletely) return false;

            bool setvector = parked && alreadyparked && setTOV;
            bool gotvector = totalVTThrprecision.Round(1) == 100 && tgotTOV > 0.25;
            parkedcompletely = setvector && gotvector;

            bool pendingrotation = setvector && !gotvector;
            bool parking = parked && !alreadyparked;
            bool unparking = !parked && alreadyparked;

            if (parking || (unparking && BlockManager.Doneloop) || changedpark)
            {
                ResetParkingSeq();
            }
            if (parkedcompletely || (unparking && !BlockManager.Doneloop))
            {
                BlockManager.Run();
            }

            if (unparkedcompletely) forceunpark = false;
            return parkedcompletely;
        }

        /*string Spinner = "";

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
        }*/

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
                if (justCompiled)
                    try
                    {
                        CMinputs = Me.GetValue<Dictionary<string, object>>("ControlModule.Inputs");

                        Me.SetValue<string>("ControlModule.AddInput", dampenersButton); //Z
                        Me.SetValue<string>("ControlModule.AddInput", cruiseButton); //R
                        Me.SetValue<string>("ControlModule.AddInput", gearButton); //Shift
                        Me.SetValue<string>("ControlModule.AddInput", allowparkButton); //X

                        Me.SetValue<bool>("ControlModule.RunOnInput", true);
                        Me.SetValue<int>("ControlModule.InputState", 1);
                        Me.SetValue<float>("ControlModule.RepeatDelay", 0.016f);
                    }
                    catch
                    {
                        controlModule = false;
                    }

                if (CMinputs != null)
                {
                    // non-movement controls
                    if (!dampenersIsPressed && CMinputs.ContainsKey(dampenersButton))
                    {//inertia dampener key
                        dampeners = !dampeners;//toggle
                        dampenersIsPressed = true;
                    }
                    else if (dampenersIsPressed && !CMinputs.ContainsKey(dampenersButton))
                    {
                        dampenersIsPressed = false;
                    }

                    if (!cruiseIsPressed && CMinputs.ContainsKey(cruiseButton))
                    {//cruise key
                        cruise = !cruise;//toggle
                        cruiseIsPressed = true;
                    }
                    else if (cruiseIsPressed && !CMinputs.ContainsKey(cruiseButton))
                    {
                        cruiseIsPressed = false;
                    }

                    if (!gearIsPressed && CMinputs.ContainsKey(gearButton))
                    {//throttle up
                        if (gear == Accelerations.Count - 1) gear = 0;
                        else gear++;
                        gearIsPressed = true;
                    }
                    else if (gearIsPressed && !CMinputs.ContainsKey(gearButton))
                    { //increase target acceleration
                        gearIsPressed = false;
                    }

                    if (!allowparkIsPressed && CMinputs.ContainsKey(allowparkButton))
                    {//throttle down
                        allowpark = !allowpark;
                        allowparkIsPressed = true;
                    }
                    else if (allowparkIsPressed && !CMinputs.ContainsKey(allowparkButton))
                    { //increase target acceleration
                        allowparkIsPressed = false;
                    }
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
                else if (arg.Contains(cruiseArg) && !parked)
                {
                    cruise = !cruise;
                    cruisebyarg = cruise;
                }
                else if (arg.Contains(gearArg))
                {
                    if (gear == Accelerations.Count - 1) gear = 0;
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

                        dampeners = FilterThis(mainController.TheBlock) ? mainController.TheBlock.DampenersOverride : dampeners;

                        foreach (ShipController cont in controlledControllers)
                        {
                            cont.TheBlock.DampenersOverride = dampeners;
                            cont.SetDampener(dampeners);
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
            if (justCompiled) return;

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
                    isstation = rotorsstopped = false;

                    foreach (VectorThrust n in vectorthrusters)
                        n.ActiveList(Override: true);

                    ChangeRuntime(0);
                }

                if (this.oldMass == bm) return; //modifying variables here may cause to the handler to restart every single time

                this.oldMass = bm; //else:
            }
            OneRunMainChecker(false);
        }

        bool EndBM(bool scanned, bool changedruntime)
        {
            if (scanned && parked)
            {
                BlockManager.Doneloop = true;
                ChangeRuntime(PerformanceWhilePark && wgv == 0 && !changedruntime ? 2 : 1);
            }
            else if (!parked)
            {
                parkedwithcn = alreadyparked = false;
            }
            return true;
        }

        void ChangeRuntime(int n = 0)
        {
            switch (n)
            {
                case 0: Runtime.UpdateFrequency = UpdateFrequency.Update1; break;
                case 1: Runtime.UpdateFrequency = UpdateFrequency.Update10; break;
                case 2: Runtime.UpdateFrequency = UpdateFrequency.Update100; break;
                case 3: Runtime.UpdateFrequency = UpdateFrequency.Once; break;
                case 4: Runtime.UpdateFrequency = UpdateFrequency.None; break;
                //case 5: Runtime.UpdateFrequency = update_frequency;  break;
            };
        }
    }
}
