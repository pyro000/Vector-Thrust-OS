using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace IngameScript
{
    partial class Program
    {
        void ResetParkingSeq()
        {
            BatteryStats.Start();
            BlockManager.Start();
            BlockManager.Doneloop = false;
            if (parked && !alreadyparked) alreadyparked = true;
            else if (!parked && alreadyparked) ChangeRuntime();
            thrustOn = !parked;
            if (!parked) trulyparked = false; //Careful in setting this to anything than false
        }

        void ResetVTHandlers()
        {
            if (check) log.AppendNR("Checking Blocks Again");
            GetScreen.Start();
            GetControllers.Start();
            GetVectorThrusters.Start();
            CheckParkBlocks.Start();
            MainChecker.Start();
        }

        public IEnumerable<int> GetScreensSeq()
        {
            while (true)
            {
                if (check) log.AppendNR($"  Getting Screens => new:{input_screens.Count}\n");
                if (input_screens.Any())
                {
                    this.screens.AddRange(input_screens);
                    LND(ref screens); // TODO: Check if this is worth dealing with (It can be)
                    input_screens.Clear();
                    if (pauseseq) yield return FramesBetweenActions;
                }

                if (Me.SurfaceCount > 0)
                {
                    AddSurfaceProvider(Me);
                    // this isn't really the right place to put this, but doing it right would be a lot more code (moved here temporarily)
                }

                foreach (IMyTextPanel screen in this.screens)
                {
                    bool cond1 = surfaces./*Contains*/Any(x => x.surface.Equals(screen));
                    bool cond2 = screen.IsWorking;
                    bool cond3 = screen.CustomName.ToLower().Contains(LCDName.ToLower());
                    bool cond4 = screen.Closed;

                    if (!cond1 && cond2 && cond3) surfaces.Add(new Surface(screen, this));
                    else if (cond1 && (!cond2 || !cond3 || cond4)) { 
                        //surfaces.Remove(screen);

                        List<Surface> tempsurf = surfaces.FindAll(x => x.surface.Equals(screen));
                        foreach (Surface s in tempsurf)
                        {
                            surfaces.Remove(s);
                        }
                    }

                    if (pauseseq) yield return FramesBetweenActions;
                }

                if (check)
                {
                    if (pauseseq) yield return FramesBetweenActions;
                    log.AppendNR($"  ->Done. Total Screens {screens.Count} => Total Surfaces:{surfaces.Count}\n");
                    LND(ref surfaces); //just in case
                }

                WH.Surfaces = surfaces;
                GetScreen.Doneloop = true;
                yield return FramesBetweenActions;
            }
        }
        IEnumerable<int> GetControllersSeq()
        {
            while (true)
            {
                bool greedy = this.greedy || this.applyTags;

                if (this.controllers_input.Count > 0)
                {
                    this.controllers.AddRange(controllers_input);
                    LND(ref controllers);
                    controllers_input.Clear();
                    if (pauseseq) yield return FramesBetweenActions;
                }

                StringBuilder reason = new StringBuilder();
                foreach (ShipController s in this.controllers)
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
                    if (!greedy && !HasTag(s.TheBlock))
                    {
                        currreason.AppendLine("  Doesn't match my tag\n");
                        canAdd = false;
                    }

                    if (canAdd)
                    {
                        AddSurfaceProvider(s.TheBlock); // TODO, THIS ONLY DETECTS COCKPITS (I think it's fixed)
                        List<IMyThrust> cthrs = new List<IMyThrust>();
                        if (pauseseq) yield return FramesBetweenActions;
                        GridTerminalSystem.GetBlocksOfType(cthrs, x => s.TheBlock.FilterThis(x) && !s.nThrusters.Contains(x));
                        if (pauseseq) yield return FramesBetweenActions;
                        s.nThrusters = s.nThrusters.Concat(cthrs).ToList();
                        if (pauseseq) yield return FramesBetweenActions;
                        cthrs.RemoveAll(x => x.Orientation.Forward != s.TheBlock.Orientation.Forward);    
                        if (pauseseq) yield return FramesBetweenActions;
                        s.cruiseThrusters = s.cruiseThrusters.Concat(cthrs).ToList();
                        if (pauseseq) yield return FramesBetweenActions;
                      

                        s.Dampener = s.nThrusters.Count > 0 ? s.TheBlock.DampenersOverride : dampeners;

                        if (!controlledControllers.Contains(s))
                        {
                            controlledControllers.Add(s);
                            ccontrollerblocks.Add(s.TheBlock);
                        }
                        //mainController = s; //temporal
                        /*if (s.theBlock.IsUnderControl)
						{
							controlledControllers.Add(s);
						}*/

                        if (this.applyTags)
                        {
                            AddTag(s.TheBlock);
                        }
                        if (pauseseq) yield return FramesBetweenActions;
                    }
                    else
                    {
                        RemoveSurfaceProvider(s.TheBlock);
                        controlledControllers.Remove(s);
                        ccontrollerblocks.Remove(s.TheBlock);
                        reason.Append(currreason);
                    }
                }

                if (pauseseq) yield return FramesBetweenActions;

                if (controllers.Count == 0) reason.AppendLine("Any Controller Found.\nEither for missing tag, not working or removed.");
                if (controlledControllers.Count == 0)
                {
                    log.AppendNR("ERROR: no usable ship controller found. Reason: \n");
                    log.AppendNR(reason.ToString());
                    ManageTag(true);
                    yield return FramesBetweenActions;
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
                        if (pauseseq) yield return FramesBetweenActions;
                    }
                    if (mainController == null)
                    {
                        mainController = controlledControllers[0];
                    }
                }

                GetControllers.Doneloop = true;
                yield return FramesBetweenActions;
            }
        }
        public IEnumerable<int> GetVectorThrustersSeq()
        {
            while (true)
            {

                bool greedy = this.applyTags || this.greedy;

                log.AppendNR("  >Getting Rotors\n");

                // makes this.nacelles out of all valid rotors
                foreach (IMyTerminalBlock r in vtrotors)
                {
                    if (this.applyTags)
                    {
                        AddTag(r);
                    }
                    if (pauseseq) yield return FramesBetweenActions;
                }

                foreach (IMyTerminalBlock tr in vtthrusters)
                {
                    if (this.applyTags)
                    {
                        AddTag(tr);
                    }
                    if (pauseseq) yield return FramesBetweenActions;
                }

                rotors_input.AddRange(abandonedrotors);
                if (pauseseq) yield return FramesBetweenActions;
                thrusters_input.AddRange(abandonedthrusters);
                if (pauseseq) yield return FramesBetweenActions;
                LND(ref rotors_input);
                if (pauseseq) yield return FramesBetweenActions;
                LND(ref thrusters_input);
                if (pauseseq) yield return FramesBetweenActions;

                foreach (IMyMotorStator current in rotors_input)
                {
                    if (this.applyTags)
                    {
                        AddTag(current);
                    }

                    if (current.Top != null && (greedy || HasTag(current)) && current.TopGrid != Me.CubeGrid)
                    {
                        Rotor rotor = new Rotor(current, this);
                        this.vectorthrusters.Add(new VectorThrust(rotor, this));
                        vtrotors.Add(current);
                    }
                    else
                    {
                        RemoveTag(current);
                    }
                    if (pauseseq) yield return FramesBetweenActions;
                }

                log.AppendNR("  >Getting Thrusters\n");
                // add all thrusters to their corrisponding nacelle and remove this.nacelles that have none
                for (int i = this.vectorthrusters.Count - 1; i >= 0; i--)
                {
                    IMyMotorStator temprotor = this.vectorthrusters[i].rotor.TheBlock;
                    for (int j = thrusters_input.Count - 1; j >= 0; j--)
                    {
                        bool added = false;


                        if (greedy || HasTag(thrusters_input[j]))
                        {

                            bool cond = thrusters_input[j].CubeGrid == this.vectorthrusters[i].rotor.TheBlock.TopGrid;
                            bool cond2 = vectorthrusters[i].thrusters.Any(x => x.TheBlock == thrusters_input[j]);

                            // thruster is not for the current nacelle
                            if (cond && this.applyTags)
                            {
                                AddTag(thrusters_input[j]);
                            }
                            //doesn't add it if it already exists

                            if (cond && !cond2)
                            {
                                if (justCompiled)
                                {
                                    thrusters_input[j].ThrustOverridePercentage = 0;
                                    thrusters_input[j].Enabled = true;
                                }

                                added = true;
                                abandonedthrusters.Remove(thrusters_input[j]);
                                this.vectorthrusters[i].thrusters.Add(new Thruster(thrusters_input[j], this));
                                vtthrusters.Add(thrusters_input[j]);
                                thrusters_input.RemoveAt(j);// shorten the list we have to check (It discards thrusters for next nacelle)
                            }
                        }

                        if (!added && !abandonedthrusters.Contains(thrusters_input[j]))
                            abandonedthrusters.Add(thrusters_input[j]);
                        if (pauseseq) yield return FramesBetweenActions;
                    }

                    // remove this.nacelles (rotors) without thrusters
                    if (this.vectorthrusters[i].thrusters.Count == 0)
                    {
                        if (!abandonedrotors.Contains(temprotor)) abandonedrotors.Add(temprotor);
                        vtrotors.Remove(temprotor);
                        RemoveTag(temprotor);
                        this.vectorthrusters.RemoveAt(i);// there is no more reference to the rotor, should be garbage collected (NOT ANYMORE, Added to abandoned rotors)
                    }
                    else
                    {
                        // if its still there, setup the nacelle
                        if (justCompiled)
                        {
                            temprotor.Brake();
                            temprotor.RotorLock = false;
                            temprotor.Enabled = true;
                        }

                        abandonedrotors.Remove(temprotor);
                        this.vectorthrusters[i].ValidateThrusters();
                        this.vectorthrusters[i].DetectThrustDirection();
                        this.vectorthrusters[i].AssignGroup();
                    }
                    if (pauseseq) yield return FramesBetweenActions;
                }

                log.AppendNR("  >Grouping VTThrs\n");

                if (VTThrGroups.Count == 0)
                {
                    log.AppendNR("  > [ERROR] => Any Vector Thrusters Found!\n");
                    error = true;
                    ManageTag(true);
                    if (pauseseq) yield return FramesBetweenActions;
                }

                for (int i = 0; i < VTThrGroups.Count; i++)
                {
                    VTThrGroups[i] = VTThrGroups[i].OrderByDescending(o => o.thrusters.Sum(x => x.TheBlock.MaxEffectiveThrust)).ToList();
                    if (pauseseq) yield return FramesBetweenActions;
                }

                tets = new List<double>(VTThrGroups.Count);
                tets.AddRange(Enumerable.Repeat(0.0, VTThrGroups.Count));
                if (pauseseq) yield return FramesBetweenActions;

                thrusters_input.Clear();
                rotors_input.Clear();
                GetVectorThrusters.Doneloop = true;
                yield return FramesBetweenActions;
            }
        }
        public IEnumerable<int> CheckParkBlocksSeq()
        { //this is executed only if there's not new mass
            while (true)
            {
                foreach (IMyShipConnector cn in connectorblocks)
                {
                    if (((AutoAddGridConnectors && FilterThis(cn)) || HasTag(cn)) && !connectors.Contains(cn))
                    {
                        yield return FramesBetweenActions;
                        connectors.Add(cn);
                        yield return FramesBetweenActions;
                        log.AppendNR($"New CON: {cn.CustomName}\n");
                        yield return FramesBetweenActions;
                    }
                    yield return FramesBetweenActions;
                }

                foreach (IMyLandingGear lg in landinggearblocks)
                {
                    if (((AutoAddGridLandingGears && FilterThis(lg)) || HasTag(lg)) && !landinggears.Contains(lg))
                    {
                        yield return FramesBetweenActions;
                        landinggears.Add(lg);
                        yield return FramesBetweenActions;
                        log.AppendNR($"New LanGear: {lg.CustomName}\n");
                        yield return FramesBetweenActions;
                    }
                    yield return FramesBetweenActions;
                }

                for (int i = normalbats.Count - 1; i >= 0; i--)
                {
                    IMyBatteryBlock b = normalbats[i];
                    yield return FramesBetweenActions;
                    if (HasTag(b))
                    {
                        yield return FramesBetweenActions;
                        log.AppendNR($"Filtered Bat: {b.CustomName}\n");
                        yield return FramesBetweenActions;
                        normalbats.RemoveAt(i);
                        yield return FramesBetweenActions;
                        if (!taggedbats.Contains(b)) taggedbats.Add(b);
                        yield return FramesBetweenActions;
                    }
                    yield return FramesBetweenActions;
                }

                for (int i = taggedbats.Count - 1; i >= 0; i--)
                {
                    IMyBatteryBlock b = taggedbats[i];
                    yield return FramesBetweenActions;
                    if (!HasTag(b))
                    {
                        yield return FramesBetweenActions;
                        log.AppendNR($"Filtered TagBat: {b.CustomName}\n");
                        yield return FramesBetweenActions;
                        taggedbats.RemoveAt(i);
                        yield return FramesBetweenActions;
                        if (FilterThis(b) && !normalbats.Contains(b)) normalbats.Add(b);
                        else if (!batteriesblocks.Contains(b)) batteriesblocks.Add(b);
                        yield return FramesBetweenActions;
                    }
                    yield return FramesBetweenActions;
                }

                for (int i = batteriesblocks.Count - 1; i >= 0; i--)
                {
                    IMyBatteryBlock b = batteriesblocks[i];
                    yield return FramesBetweenActions;
                    if (HasTag(b))
                    {
                        yield return FramesBetweenActions;
                        log.AppendNR($"Added TagBat: {b.CustomName}\n");
                        yield return FramesBetweenActions;
                        batteriesblocks.RemoveAt(i);
                        yield return FramesBetweenActions;
                        if (!taggedbats.Contains(b)) taggedbats.Add(b);
                        yield return FramesBetweenActions;
                    }
                    yield return FramesBetweenActions;
                }

                for (int i = connectors.Count - 1; i >= 0; i--)
                {
                    IMyShipConnector c = connectors[i];
                    yield return FramesBetweenActions;
                    bool hastag = HasTag(c);
                    yield return FramesBetweenActions;

                    if ((!AutoAddGridConnectors && !hastag) || (AutoAddGridConnectors && !hastag && !FilterThis(c)))
                    {
                        yield return FramesBetweenActions;
                        log.AppendNR($"Filtered Con: {c.CustomName}\n");
                        yield return FramesBetweenActions;
                        connectors.RemoveAt(i);
                        yield return FramesBetweenActions;
                        forceunpark = true;
                        yield return FramesBetweenActions;
                    }
                    yield return FramesBetweenActions;
                }

                for (int i = landinggears.Count - 1; i >= 0; i--)
                {
                    IMyLandingGear l = landinggears[i];
                    yield return FramesBetweenActions;
                    if ((AutoAddGridLandingGears && !HasTag(l) && !FilterThis(l)) || (!AutoAddGridLandingGears && !HasTag(l)))
                    {
                        yield return FramesBetweenActions;
                        log.AppendNR($"Filtered LanGear: {l.CustomName}\n");
                        yield return FramesBetweenActions;
                        landinggears.RemoveAt(i);
                        yield return FramesBetweenActions;
                        forceunpark = true;
                        yield return FramesBetweenActions;
                    }
                    yield return FramesBetweenActions;
                }

                for (int i = 0; i < VTThrGroups.Count; i++)
                {
                    VTThrGroups[i] = VTThrGroups[i].OrderByDescending(o => o.totalEffectiveThrust).ToList();
                    yield return FramesBetweenActions;
                }

                CheckParkBlocks.Doneloop = true;
                yield return FramesBetweenActions;
            }

        }

        void AddBlock<T>(IMyTerminalBlock block, ref List<T> list) 
        {
            if (!AllBlocks.Contains(block)) AllBlocks.Add(block);
            if (!list.Contains((T)block)) list.Add((T)block);
            blockcount++;
        }
        //bool deleting = false;

        public IEnumerable<int> CheckVectorThrustersSeq()
        {
            while (true)
            {
                pauseseq = ((!justCompiled || (justCompiled && error)) && !applyTags);
                if (pauseseq) yield return FramesBetweenActions;
                if (!check)
                {
                    if (GetControllers.Loop(pauseseq)) yield return FramesBetweenActions;
                    if (GetScreen.Loop(pauseseq)) yield return FramesBetweenActions;
                    if (CheckParkBlocks.Loop(pauseseq)) yield return FramesBetweenActions;

                    log.AppendNR(" -Everything seems normal.");
                    continue;
                }

                if (!justCompiled) log.AppendNR("  -Mass is different\n");

                List<IMyTerminalBlock> NewBlocks = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocks(NewBlocks);

                if (!applyTags && NewBlocks.Count.Equals(blockcount)) {
                    check = false;
                    yield return FramesBetweenActions;
                    continue;
                }

                if (!justCompiled) log.AppendNR("  -New blocks detected\n");

                List<IMyTerminalBlock> vtblocks = new List<IMyTerminalBlock>(vtthrusters)
                    .Concat(normalThrusters).Concat(vtrotors).Concat(controllerblocks).ToList();

                bool search = vtblocks.Any(x => !GridTerminalSystem.CanAccess(x));

                if (search)
                {
                    foreach (IMyTerminalBlock b in vtblocks)
                    {
                        if (!GridTerminalSystem.CanAccess(b))
                        {
                            AllBlocks.Remove(b);
                            blockcount--;

                            if (b is IMyShipController)
                            {
                                IMyShipController co = (IMyShipController)b;

                                ccontrollerblocks.Remove(co);
                                controllerblocks.Remove(co);
                                RemoveSurfaceProvider(co);
                                controlledControllers.Remove(controlledControllers.Find(x => x.TheBlock.Equals(co)));
                                controllers.Remove(controllers.Find(x => x.TheBlock.Equals(co)));

                                if (controlledControllers.Empty())
                                {
                                    log.AppendNR($"ERROR -> Any Usable Controller Found, Shutting Down");
                                    ManageTag(true);
                                    error = true;
                                    yield return FramesBetweenActions;
                                }

                                mainController = controlledControllers[0];
                            }
                            else if (b is IMyThrust)
                            {
                                IMyThrust tr = (IMyThrust)b;

                                abandonedthrusters.Remove(tr);
                                cruiseThr.Remove(tr);
                                bool oldnthr = !normalThrusters.Empty();
                                normalThrusters.Remove(tr);

                                if (normalThrusters.Empty() && oldnthr)
                                {
                                    dampeners = true; //Put dampeners back on if normalthrusters got removed entirely
                                }
                                vtthrusters.Remove(tr);
                            }
                            else
                            {
                                vtrotors.Remove((IMyMotorStator)b);
                                abandonedrotors.Remove((IMyMotorStator)b);
                            }
                        }
                    }
                }


                List<IMyTerminalBlock> newvtblocks = new List<IMyTerminalBlock>(NewBlocks)
                    .FindAll(x => x is IMyThrust || x is IMyMotorStator || x is IMyShipController)
                    .Except(normalThrusters).Except(vtrotors).Except(vtthrusters).Except(controllerblocks).ToList();

                if (applyTags || !newvtblocks.Empty()) { 
                    foreach (IMyTerminalBlock b in newvtblocks)
                    {
                        if (b is IMyShipController)
                        {
                            AddBlock(b, ref controllerblocks); 
                            controllers_input.Add(new ShipController((IMyShipController)b, this));
                        }
                        else if (b is IMyThrust)
                        {
                            if (!FilterThis(b)) AddBlock(b, ref thrusters_input); 
                            else
                            {
                                IMyThrust tr = (IMyThrust)b;
                                AddBlock(b, ref normalThrusters); 
                                if (b.Orientation.Forward == mainController.TheBlock.Orientation.Forward) //changing
                                {
                                    cruiseThr.Add(tr);
                                    //log.AppendNR("Added back thrust: " + b.CustomName);
                                }
                                if (!justCompiled && stockvalues) (b as IMyFunctionalBlock).Enabled = true;
                            }
                        }
                        else
                        {
                            AddBlock(b, ref rotors_input);
                        }
                    }

                    GetControllers.Loop(false);
                    GetVectorThrusters.Loop(false);

                    //if (GetControllers.Loop(pauseseq)) yield return timepause;
                    //if (GetVectorThrusters.Loop(pauseseq)) yield return timepause;
                }

                if (pauseseq) yield return FramesBetweenActions;

                foreach (VectorThrust vt in vectorthrusters)
                {
                    vt.thrusters.RemoveAll(x => !vtthrusters.Contains(x.TheBlock));
                    vt.activeThrusters.RemoveAll(x => !vt.thrusters.Contains(x));
                    vt.availableThrusters.RemoveAll(x => !vt.thrusters.Contains(x));
                    if (pauseseq) yield return FramesBetweenActions;
                }


                for (int i = vectorthrusters.Count - 1; i >= 0; i--) {
                    VectorThrust vt = vectorthrusters[i];
                    IMyMotorStator rt = vt.rotor.TheBlock;

                    if (!vtrotors.Contains(rt) || rt.Top == null || vt.thrusters.Empty()) {

                        rt.Brake();
                        vectorthrusters.RemoveAt(i);
                        if (!abandonedrotors.Contains(rt)) abandonedrotors.Add(rt);
                    }
                    if (pauseseq) yield return FramesBetweenActions;
                }

                foreach (List<VectorThrust> group in VTThrGroups)
                {
                    group.RemoveAll(x => !vectorthrusters.Contains(x) || x.thrusters.Count < 1);
                    if (pauseseq) yield return FramesBetweenActions;
                }

                VTThrGroups.RemoveAll(x => x.Count < 1);
                if (pauseseq) yield return FramesBetweenActions;

                for (int i = AllBlocks.Count - 1; i >= 0; i--)
                {
                    IMyTerminalBlock b = AllBlocks[i];

                    bool tagallcond = TagAll && (b is IMyBatteryBlock || b is IMyGasTank || b is IMyLandingGear || b is IMyShipConnector);
                    bool tagcond = b is IMyShipController || vtthrusters.Contains(b) || b is IMyMotorStator;

                    if (!GridTerminalSystem.CanAccess(b))
                    {
                        AllBlocks.RemoveAt(i);

                        if (b is IMyLandingGear)
                        {
                            landinggearblocks.Remove((IMyLandingGear)b);
                            landinggears.Remove((IMyLandingGear)b);
                        }
                        else if (b is IMyShipConnector)
                        {
                            connectorblocks.Remove((IMyShipConnector)b);
                            connectors.Remove((IMyShipConnector)b);
                        }
                        else if (b is IMyGasTank)
                        {
                            tankblocks.Remove((IMyGasTank)b);
                        }
                        else if (b is IMyBatteryBlock)
                        {
                            batteriesblocks.Remove((IMyBatteryBlock)b);
                            taggedbats.Remove((IMyBatteryBlock)b);
                            normalbats.Remove((IMyBatteryBlock)b);
                        }
                        else if (b is IMyTextPanel)
                        {
                            screens.Remove((IMyTextPanel)b);
                            RemoveSurface((IMyTextPanel)b);
                        }
                        else if (b is IMySoundBlock)
                        {
                            soundblocks.Remove((IMySoundBlock)b);
                        }

                    }
                    else if (applyTags && (tagallcond || tagcond))
                    {
                        AddTag(b);
                        if (RenameBackupSubstring && b.BlockDefinition.SubtypeId.Contains("SmallBattery") && !b.CustomName.Contains(BackupSubstring))
                        {
                            b.CustomName += " " + BackupSubstring;
                        }
                    }

                    if (pauseseq) yield return FramesBetweenActions;
                };

                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>(NewBlocks).Except(AllBlocks).ToList();
                if (pauseseq) yield return FramesBetweenActions;

                foreach (IMyTerminalBlock b in blocks)
                {
                    if (GridTerminalSystem.CanAccess(b))
                    {
                        bool island = b is IMyLandingGear;
                        bool iscon = b is IMyShipConnector;
                        bool samegrid = FilterThis(b);
                        bool hastag = HasTag(b);
                        bool xor = samegrid || hastag;

                       if (b is IMyTextPanel)
                        {
                            AddBlock(b, ref input_screens);
                        }
                        else if (iscon)
                        {
                            if (TagAll) AddTag(b);
                            bool cond1 = AutoAddGridConnectors && xor;
                            bool cond2 = !AutoAddGridConnectors && hastag;

                            bool cncond = cond1 || cond2;

                            AddBlock(b, ref connectorblocks); 
                            if (cncond && !connectors.Contains(b)) connectors.Add((IMyShipConnector)b);
                        }
                        else if (island)
                        {
                            if (TagAll) AddTag(b);
                            bool cond3 = AutoAddGridLandingGears && xor;
                            bool cond4 = !AutoAddGridLandingGears && hastag;

                            bool lgcond = cond3 || cond4;

                            AddBlock(b, ref landinggearblocks);
                            if (lgcond && !landinggears.Contains(b)) landinggears.Add((IMyLandingGear)b);
                        }
                        else if (b is IMyGasTank && (hastag || TagAll || samegrid))
                        {
                            if (TagAll) AddTag(b);
                            AddBlock(b, ref tankblocks);
                            if (hastag && stockvalues) (b as IMyGasTank).Stockpile = false;
                        }
                        else if (b is IMyBatteryBlock)
                        {
                            IMyBatteryBlock bat = (IMyBatteryBlock)b;

                            if (TagAll) { 
                                AddTag(b);
                                //log.AppendNR("Cacoide1"); TODO, THIS SEEMS TO NOT HAVE ANY EFFECT
                                if (RenameBackupSubstring && b.BlockDefinition.SubtypeId.Contains("SmallBattery") && !b.CustomName.Contains(BackupSubstring)) { 
                                    b.CustomName += " " + BackupSubstring;
                                    //log.AppendNR("Cacoide");
                                }
                            }
                            if (justCompiled && (hastag || samegrid) && stockvalues) bat.ChargeMode = ChargeMode.Auto;

                            if (hastag) AddBlock(b, ref taggedbats);
                            else if (samegrid) AddBlock(b, ref normalbats); 
                            else AddBlock(b, ref batteriesblocks);
                        }
                        else if (b is IMySoundBlock && hastag)
                        {
                            IMySoundBlock sb = (IMySoundBlock)b;
                            sb.LoopPeriod = 1;
                            sb.SelectedSound = "Alert 2";
                            AddBlock(b, ref soundblocks); 
                        }
                    }
                    if (pauseseq) yield return FramesBetweenActions;
                }

                if (!justCompiled && CheckParkBlocks.Loop(pauseseq)) yield return FramesBetweenActions; //Causes script too complex if done in one run

                if (GetScreen.Loop(pauseseq)) yield return FramesBetweenActions;

                // TODO: Investigate if this is necessary
                LND(ref controllerblocks);
                if (pauseseq) yield return FramesBetweenActions;

                LND(ref vectorthrusters);
                if (pauseseq) yield return FramesBetweenActions;

                LND(ref normalThrusters);
                if (pauseseq) yield return FramesBetweenActions;

                LND(ref vtthrusters);
                if (pauseseq) yield return FramesBetweenActions;

                LND(ref vtrotors);
                if (pauseseq) yield return FramesBetweenActions;

                LND(ref ccontrollerblocks);
                if (pauseseq) yield return FramesBetweenActions;

                LND(ref controlledControllers);
                if (pauseseq) yield return FramesBetweenActions;

                check = false;
                blockcount = NewBlocks.Count;
                yield return FramesBetweenActions;
            }
        }


        public IEnumerable<int> GetBatStatsSeq()
        {
            while (true)
            {
                outputbatsseq.Clear();
                if (batsseq.Count > 0)
                {
                    double inputs = 0;
                    double outputs = 0;
                    double percents = 0;
                    foreach (IMyPowerProducer b in batsseq)
                    {
                        outputs += b.CurrentOutput;
                        if (b is IMyBatteryBlock)
                        {
                            inputs += (b as IMyBatteryBlock).CurrentInput;
                            percents += (b as IMyBatteryBlock).CurrentStoredPower / (b as IMyBatteryBlock).MaxStoredPower;
                        }

                        outputs -= b.MaxOutput;
                        yield return FramesBetweenActions;
                    }
                    inputs /= inputs != 0 ? batsseq.Count : 1;
                    outputs /= outputs != 0 ? batsseq.Count : 1;
                    percents *= percents != 0 ? (100 / batsseq.Count) : 1;

                    outputbatsseq = new List<double> { inputs, outputs, percents.Round(0) };
                    yield return FramesBetweenActions;
                }

                BatteryStats.Doneloop = true;
                yield return FramesBetweenActions;
            }
        }



        public IEnumerable<int> BlockManagerSeq()
        {
            List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
            List<IMyBatteryBlock> backupbatteries = new List<IMyBatteryBlock>();
            bool setthr = false;
            bool donescan = false;
            bool changedruntime = false;

            while (true)
            {
                bool turnoffthr = TurnOffThrustersOnPark && !normalThrusters.Empty();

                if (turnoffthr && (!setthr || !parked))
                {
                    normalThrusters.ForEach(x => x.Enabled = !parked);
                    setthr = true;
                }

                if ((normalbats.Count + taggedbats.Count < 2) || !RechargeOnPark || !parkedwithcn)
                {
                    changedruntime = EndBM(true, changedruntime);
                    yield return FramesBetweenActions;
                    continue;
                }

                if (batteries.Empty() && backupbatteries.Empty())
                {
                    List<IMyBatteryBlock> allbats = new List<IMyBatteryBlock>(taggedbats).Concat(normalbats).ToList();
                    if (parked) yield return FramesBetweenActions;
                    backupbatteries = allbats.FindAll(x => x.CustomName.Contains(BackupSubstring) || (greedy && RenameBackupSubstring && x.BlockDefinition.SubtypeId.Contains("SmallBattery")));
                    if (parked) yield return FramesBetweenActions;

                    if (!backupbatteries.Empty() && allbats.SequenceEqual(backupbatteries))
                    {
                        batteries = new List<IMyBatteryBlock>(backupbatteries);
                        backupbatteries = new List<IMyBatteryBlock> { batteries[0] };
                        batteries.RemoveAt(0);
                    }
                    else if (!backupbatteries.Empty())
                    {
                        batteries = allbats.Except(backupbatteries).ToList();
                    }

                    else if (backupbatteries.Empty() && taggedbats.Count > normalbats.Count)
                    {
                        backupbatteries = new List<IMyBatteryBlock>(normalbats);

                        if (normalbats.Empty())
                        {
                            backupbatteries = new List<IMyBatteryBlock> { taggedbats[0] };
                        }

                        batteries = new List<IMyBatteryBlock>(taggedbats).Except(backupbatteries).ToList();
                    }
                    else if (backupbatteries.Empty())
                    {
                        backupbatteries.Add(normalbats.Empty() ? taggedbats[0] : normalbats[0]);
                        batteries = batteries.Concat(normalbats).Concat(taggedbats).Except(backupbatteries).ToList();
                    }
                    //Getting at least 1 bat/tank to handle thrusters for a bit
                    if (!parked)
                    {
                        if (!batteries.Empty()) batteries[0].ChargeMode = ChargeMode.Auto;
                        if (!tankblocks.Empty()) tankblocks[0].Stockpile = false;
                    }
                    yield return FramesBetweenActions;
                }

                List<IMyPowerProducer> pw = new List<IMyPowerProducer>();
                GridTerminalSystem.GetBlocksOfType(pw, x => !batteries.Contains(x) && !backupbatteries.Contains(x));
                yield return FramesBetweenActions;

                List<double> statsBBATS = new List<double>();
                List<double> statsPW = new List<double>();
                yield return FramesBetweenActions;

                if (!donescan || (BlockManager.Doneloop && parked))
                {
                    if (parked)
                    { //temporary fix
                        batsseq = new List<IMyTerminalBlock>(pw);
                        while (!BatteryStats.Doneloop)
                        {
                            BatteryStats.Run();
                            yield return FramesBetweenActions;
                        }
                        BatteryStats.Doneloop = false;

                        donescan = true;
                        statsPW = new List<double>(outputbatsseq);
                    }
                    yield return FramesBetweenActions;

                    batsseq = new List<IMyTerminalBlock>(backupbatteries);
                    while (!BatteryStats.Doneloop)
                    {
                        BatteryStats.Run();
                        yield return FramesBetweenActions;
                    }
                    BatteryStats.Doneloop = false;

                    statsBBATS = new List<double>(outputbatsseq);
                    yield return FramesBetweenActions;
                }

                bool lowbackupbat = !statsBBATS.Empty() && statsBBATS[2] < 2.5;
                bool comebackupbat = !statsBBATS.Empty() && statsBBATS[2] > 25;
                yield return FramesBetweenActions;

                bool charging = batteries.All(x => x.ChargeMode == ChargeMode.Recharge);
                rechargecancelled = (statsPW.Empty() && donescan) || (!statsPW.Empty() && statsPW[1] == 0) || lowbackupbat;
                bool notcharged = parked && parkedwithcn && donescan && ((!charging && !rechargecancelled) || (charging && rechargecancelled)) && !batteries.Empty();
                bool reassign = rechargecancelled && !statsPW.Empty() && statsPW[1] != 0 && comebackupbat;
                yield return FramesBetweenActions;

                if (!((!parked && parkedwithcn) || notcharged || reassign))
                {
                    changedruntime = EndBM(donescan, changedruntime);
                    yield return FramesBetweenActions;
                    continue;
                }

                foreach (IMyGasTank t in tankblocks)
                {
                    t.Stockpile = !rechargecancelled && parked && t.FilledRatio != 1;
                    yield return FramesBetweenActions;
                }

                if (parked && !rechargecancelled)
                { //If I don't do this the ship will shut off
                    foreach (IMyBatteryBlock b in backupbatteries)
                    {
                        b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Auto : ChargeMode.Recharge;
                        yield return FramesBetweenActions;
                    }
                    foreach (IMyBatteryBlock b in batteries)
                    {
                        b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Recharge : ChargeMode.Auto;
                        yield return FramesBetweenActions;
                    }
                }
                else if (!parked || rechargecancelled)
                {
                    foreach (IMyBatteryBlock b in batteries)
                    {
                        b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Recharge : ChargeMode.Auto;
                        yield return FramesBetweenActions;
                    }
                    foreach (IMyBatteryBlock b in backupbatteries)
                    {
                        b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Auto : ChargeMode.Recharge;
                        yield return FramesBetweenActions;
                    }
                }


                changedruntime = EndBM(donescan, changedruntime);
                yield return FramesBetweenActions;
            }
        }
    }
}
