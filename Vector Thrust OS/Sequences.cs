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

        public IEnumerable<double> GetScreensSeq()
        {
            while (true)
            {
                if (check) log.AppendNR($"  Getting Screens => new:{input_screens.Count}\n");
                if (input_screens.Any())
                {
                    this.screens.AddRange(input_screens);
                    LND(ref screens); // TODO: Check if this is worth dealing with (It can be)
                    input_screens.Clear();
                    if (pauseseq) yield return timepause;
                }

                if (Me.SurfaceCount > 0)
                {
                    AddSurfaceProvider(Me);
                    // this isn't really the right place to put this, but doing it right would be a lot more code (moved here temporarily)
                }

                foreach (IMyTextPanel screen in this.screens)
                {
                    bool cond1 = surfaces.Contains(screen);
                    bool cond2 = screen.IsWorking;
                    bool cond3 = screen.CustomName.ToLower().Contains(LCDName.ToLower());
                    bool cond4 = screen.Closed;

                    if (!cond1 && cond2 && cond3) surfaces.Add(screen);
                    else if (cond1 && (!cond2 || !cond3 || cond4)) surfaces.Remove(screen);

                    if (pauseseq) yield return timepause;
                }

                if (check)
                {
                    if (pauseseq) yield return timepause;
                    log.AppendNR($"  ->Done. Total Screens {screens.Count} => Total Surfaces:{surfaces.Count}\n");
                    LND(ref surfaces); //just in case
                }

                WH.Surfaces = surfaces;
                GetScreen.Doneloop = true;
                yield return timepause;
            }
        }
        IEnumerable<double> GetControllersSeq()
        {
            while (true)
            {
                bool greedy = this.greedy || this.applyTags;

                if (this.controllers_input.Count > 0)
                {
                    this.controllers.AddRange(controllers_input);
                    LND(ref controllers);
                    controllers_input.Clear();
                    if (pauseseq) yield return timepause;
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
                        AddSurfaceProvider(s.TheBlock); // TODO, THIS ONLY DETECTS COCKPITS
                        List<IMyThrust> cthrs = new List<IMyThrust>();
                        if (pauseseq) yield return timepause;
                        GridTerminalSystem.GetBlocksOfType(cthrs, x => s.TheBlock.FilterThis(x) && !s.nThrusters.Contains(x));
                        if (pauseseq) yield return timepause;
                        s.nThrusters = s.nThrusters.Concat(cthrs).ToList();
                        if (pauseseq) yield return timepause;
                        cthrs.RemoveAll(x => x.Orientation.Forward != s.TheBlock.Orientation.Forward);    
                        if (pauseseq) yield return timepause;
                        s.cruiseThrusters = s.cruiseThrusters.Concat(cthrs).ToList();
                        if (pauseseq) yield return timepause;
                      

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
                        if (pauseseq) yield return timepause;
                    }
                    else
                    {
                        RemoveSurfaceProvider(s.TheBlock);
                        controlledControllers.Remove(s);
                        ccontrollerblocks.Remove(s.TheBlock);
                        reason.Append(currreason);
                    }
                }

                if (pauseseq) yield return timepause;

                if (controllers.Count == 0) reason.AppendLine("Any Controller Found.\nEither for missing tag, not working or removed.");
                if (controlledControllers.Count == 0)
                {
                    log.AppendNR("ERROR: no usable ship controller found. Reason: \n");
                    log.AppendNR(reason.ToString());
                    ManageTag(true);
                    yield return timepause;
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
                        if (pauseseq) yield return timepause;
                    }
                    if (mainController == null)
                    {
                        mainController = controlledControllers[0];
                    }
                }

                GetControllers.Doneloop = true;
                yield return timepause;
            }
        }
        public IEnumerable<double> GetVectorThrustersSeq()
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
                    if (pauseseq) yield return timepause;
                }

                foreach (IMyTerminalBlock tr in vtthrusters)
                {
                    if (this.applyTags)
                    {
                        AddTag(tr);
                    }
                    if (pauseseq) yield return timepause;
                }

                rotors_input.AddRange(abandonedrotors);
                if (pauseseq) yield return timepause;
                thrusters_input.AddRange(abandonedthrusters);
                if (pauseseq) yield return timepause;
                LND(ref rotors_input);
                if (pauseseq) yield return timepause;
                LND(ref thrusters_input);
                if (pauseseq) yield return timepause;

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
                    if (pauseseq) yield return timepause;
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
                        if (pauseseq) yield return timepause;
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
                    if (pauseseq) yield return timepause;
                }

                log.AppendNR("  >Grouping VTThrs\n");

                if (VTThrGroups.Count == 0)
                {
                    log.AppendNR("  > [ERROR] => Any Vector Thrusters Found!\n");
                    error = true;
                    ManageTag(true);
                    if (pauseseq) yield return timepause;
                }

                for (int i = 0; i < VTThrGroups.Count; i++)
                {
                    VTThrGroups[i] = VTThrGroups[i].OrderByDescending(o => o.thrusters.Sum(x => x.TheBlock.MaxEffectiveThrust)).ToList();
                    if (pauseseq) yield return timepause;
                }

                tets = new List<double>(VTThrGroups.Count);
                tets.AddRange(Enumerable.Repeat(0.0, VTThrGroups.Count));
                if (pauseseq) yield return timepause;

                thrusters_input.Clear();
                rotors_input.Clear();
                GetVectorThrusters.Doneloop = true;
                yield return timepause;
            }
        }
        public IEnumerable<double> CheckParkBlocksSeq()
        { //this is executed only if there's not new mass
            while (true)
            {
                foreach (IMyShipConnector cn in connectorblocks)
                {
                    if (((AutoAddGridConnectors && FilterThis(cn)) || HasTag(cn)) && !connectors.Contains(cn))
                    {
                        yield return timepause;
                        connectors.Add(cn);
                        yield return timepause;
                        log.AppendNR($"New CON: {cn.CustomName}\n");
                        yield return timepause;
                    }
                    yield return timepause;
                }

                foreach (IMyLandingGear lg in landinggearblocks)
                {
                    if (((AutoAddGridLandingGears && FilterThis(lg)) || HasTag(lg)) && !landinggears.Contains(lg))
                    {
                        yield return timepause;
                        landinggears.Add(lg);
                        yield return timepause;
                        log.AppendNR($"New LanGear: {lg.CustomName}\n");
                        yield return timepause;
                    }
                    yield return timepause;
                }

                for (int i = normalbats.Count - 1; i >= 0; i--)
                {
                    IMyBatteryBlock b = normalbats[i];
                    yield return timepause;
                    if (HasTag(b))
                    {
                        yield return timepause;
                        log.AppendNR($"Filtered Bat: {b.CustomName}\n");
                        yield return timepause;
                        normalbats.RemoveAt(i);
                        yield return timepause;
                        if (!taggedbats.Contains(b)) taggedbats.Add(b);
                        yield return timepause;
                    }
                    yield return timepause;
                }

                for (int i = taggedbats.Count - 1; i >= 0; i--)
                {
                    IMyBatteryBlock b = taggedbats[i];
                    yield return timepause;
                    if (!HasTag(b))
                    {
                        yield return timepause;
                        log.AppendNR($"Filtered TagBat: {b.CustomName}\n");
                        yield return timepause;
                        taggedbats.RemoveAt(i);
                        yield return timepause;
                        if (FilterThis(b) && !normalbats.Contains(b)) normalbats.Add(b);
                        else if (!batteriesblocks.Contains(b)) batteriesblocks.Add(b);
                        yield return timepause;
                    }
                    yield return timepause;
                }

                for (int i = batteriesblocks.Count - 1; i >= 0; i--)
                {
                    IMyBatteryBlock b = batteriesblocks[i];
                    yield return timepause;
                    if (HasTag(b))
                    {
                        yield return timepause;
                        log.AppendNR($"Added TagBat: {b.CustomName}\n");
                        yield return timepause;
                        batteriesblocks.RemoveAt(i);
                        yield return timepause;
                        if (!taggedbats.Contains(b)) taggedbats.Add(b);
                        yield return timepause;
                    }
                    yield return timepause;
                }

                for (int i = connectors.Count - 1; i >= 0; i--)
                {
                    IMyShipConnector c = connectors[i];
                    yield return timepause;
                    bool hastag = HasTag(c);
                    yield return timepause;

                    if ((!AutoAddGridConnectors && !hastag) || (AutoAddGridConnectors && !hastag && !FilterThis(c)))
                    {
                        yield return timepause;
                        log.AppendNR($"Filtered Con: {c.CustomName}\n");
                        yield return timepause;
                        connectors.RemoveAt(i);
                        yield return timepause;
                        forceunpark = true;
                        yield return timepause;
                    }
                    yield return timepause;
                }

                for (int i = landinggears.Count - 1; i >= 0; i--)
                {
                    IMyLandingGear l = landinggears[i];
                    yield return timepause;
                    if ((AutoAddGridLandingGears && !HasTag(l) && !FilterThis(l)) || (!AutoAddGridLandingGears && !HasTag(l)))
                    {
                        yield return timepause;
                        log.AppendNR($"Filtered LanGear: {l.CustomName}\n");
                        yield return timepause;
                        landinggears.RemoveAt(i);
                        yield return timepause;
                        forceunpark = true;
                        yield return timepause;
                    }
                    yield return timepause;
                }

                for (int i = 0; i < VTThrGroups.Count; i++)
                {
                    VTThrGroups[i] = VTThrGroups[i].OrderByDescending(o => o.totalEffectiveThrust).ToList();
                    yield return timepause;
                }

                CheckParkBlocks.Doneloop = true;
                yield return timepause;
            }

        }

        readonly List<IMySoundBlock> soundblocks = new List<IMySoundBlock>();
        readonly List<IMyShipConnector> connectorblocks = new List<IMyShipConnector>();
        readonly List<IMyLandingGear> landinggearblocks = new List<IMyLandingGear>();
        readonly List<IMyBatteryBlock> batteriesblocks = new List<IMyBatteryBlock>();


        public IEnumerable<double> CheckVectorThrustersSeq()
        {
            while (true)
            {
                pauseseq = ((!justCompiled || (justCompiled && error)) && !applyTags);
                if (pauseseq) yield return timepause;
                if (!check)
                {
                    while (!GetControllers.Doneloop)
                    {
                        GetControllers.Run();
                        //_RuntimeTracker.RegisterAction("CheckConts");
                        yield return timepause;
                    }
                    GetControllers.Doneloop = false;

                    while (!GetScreen.Doneloop)
                    {
                        GetScreen.Run();
                        //_RuntimeTracker.RegisterAction("CheckScreen");
                        yield return timepause;
                    }
                    GetScreen.Doneloop = false;

                    while (!CheckParkBlocks.Doneloop)
                    {
                        CheckParkBlocks.Run();
                        //_RuntimeTracker.RegisterAction("CheckParkB");
                        yield return timepause;
                    }
                    CheckParkBlocks.Doneloop = false;

                    log.AppendNR(" -Everything seems normal.");
                    continue;
                }

                if (!justCompiled) log.AppendNR("  -Mass is different, checking everything\n");

                bool quick = (wgv != 0 && vtthrusters.Any(x => !GridTerminalSystem.CanAccess(x))) || controlledControllers.Any(x => !GridTerminalSystem.CanAccess(x.TheBlock));
                
                if (pauseseq) yield return timepause;

                List<IMyTerminalBlock> vtblocks = new List<IMyTerminalBlock>(vtthrusters).Concat(vtrotors).ToList();
                if (pauseseq && !quick) yield return timepause;

                foreach (IMyTerminalBlock b in vtblocks)
                {
                    if (!GridTerminalSystem.CanAccess(b))
                    {
                        if (b is IMyThrust)
                        {
                            vtthrusters.Remove((IMyThrust)b);
                        }
                        else { 
                            vtrotors.Remove((IMyMotorStator)b);
                            abandonedrotors.Remove((IMyMotorStator)b);
                        }
                    }
                    if (pauseseq && !quick) yield return timepause;
                }

                vectorthrusters.RemoveAll(x => !vtrotors.Contains(x.rotor.TheBlock));
                if (pauseseq && !quick) yield return timepause;

                foreach (VectorThrust vt in vectorthrusters)
                {
                    vt.thrusters.RemoveAll(x => !vtthrusters.Contains(x.TheBlock));
                    vt.activeThrusters.RemoveAll(x => !vt.thrusters.Contains(x));
                    vt.availableThrusters.RemoveAll(x => !vt.thrusters.Contains(x));
                    if (pauseseq) yield return timepause;
                }

                foreach (List<VectorThrust> group in VTThrGroups)
                {
                    group.RemoveAll(x => !vectorthrusters.Contains(x) || x.thrusters.Count < 1);
                    if (pauseseq) yield return timepause;
                }

                VTThrGroups.RemoveAll(x => x.Count < 1);
                if (pauseseq) yield return timepause;
                rotors_input.Clear();
                if (pauseseq) yield return timepause;
                thrusters_input.Clear();
                if (pauseseq) yield return timepause;

                List<IMyTerminalBlock> newvtblocks = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(newvtblocks, x => (x is IMyThrust && !FilterThis(x)) || x is IMyMotorStator);
                newvtblocks = newvtblocks.Except(vtrotors).Except(vtthrusters).ToList();

                if (pauseseq) yield return timepause;

                foreach (IMyTerminalBlock b in newvtblocks) {
                    if (b is IMyThrust)
                    {
                        thrusters_input.Add((IMyThrust)b);
                    }
                    else {
                        rotors_input.Add((IMyMotorStator)b);
                    }
  
                    if (pauseseq) yield return timepause;
                }

                while (!GetVectorThrusters.Doneloop)
                {
                    GetVectorThrusters.Run();
                    if (pauseseq) yield return timepause;
                }
                GetVectorThrusters.Doneloop = false;

                List<IMyTerminalBlock> allblocks = new List<IMyTerminalBlock>(connectors);
                allblocks = allblocks
                            .Concat(connectorblocks)
                            .Concat(landinggears)
                            .Concat(landinggearblocks)
                            .Concat(tankblocks)
                            .Concat(taggedbats)
                            .Concat(normalbats)
                            .Concat(batteriesblocks)
                            .Concat(cruiseThr)
                            .Concat(normalThrusters)
                            .Concat(controllerblocks)
                            .Concat(ccontrollerblocks)
                            .Concat(abandonedrotors)
                            .Concat(abandonedthrusters) // to remove deleted ones, don't panic if you don't find this variable anywhere
                            .Concat(screens)
                            .ToList();

                if (pauseseq) yield return timepause;

                foreach (IMyTerminalBlock b in allblocks)
                {
                    bool tagallcond = TagAll && (b is IMyBatteryBlock || b is IMyGasTank || b is IMyLandingGear || b is IMyShipConnector);
                    bool tagcond = b is IMyShipController || vtthrusters.Contains(b) || b is IMyMotorStator;

                    if (!GridTerminalSystem.CanAccess(b))
                    {
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
                        else if (b is IMyThrust)
                        {
                            abandonedthrusters.Remove((IMyThrust)b);

                            cruiseThr.Remove((IMyThrust)b);
                            bool oldnthr = !normalThrusters.Empty();
                            normalThrusters.Remove((IMyThrust)b);

                            if (normalThrusters.Empty() && oldnthr)
                            {
                                dampeners = true; //Put dampeners back on if normalthrusters got removed entirely
                            }
                        }
                        else if (b is IMyShipController)
                        {
                            ccontrollerblocks.Remove((IMyShipController)b);
                            controllerblocks.Remove((IMyShipController)b);
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
                        log.AppendNR("Adding tag:" + b.CustomName + "\n");
                        AddTag(b);
                    }
                    else if (b is IMyMotorStator && (b as IMyMotorStator).Top == null)
                    {
                        log.AppendNR("NO TOP: " + b.CustomName + "\n");
                        RemoveTag(b);
                        abandonedrotors.Remove((IMyMotorStator)b);
                        vtrotors.Remove((IMyMotorStator)b);
                    }

                    if (pauseseq) yield return timepause;
                };

                controllers_input.Clear();
                if (pauseseq) yield return timepause;

                controllers.RemoveAll(x => !GridTerminalSystem.CanAccess(x.TheBlock));
                if (pauseseq) yield return timepause;

                for (int i = controlledControllers.Count - 1; i >= 0; i--)
                {
                    if (!GridTerminalSystem.CanAccess(controlledControllers[i].TheBlock))
                    {
                        RemoveSurfaceProvider(controlledControllers[i].TheBlock);
                        controlledControllers.RemoveAt(i);
                    }
                    if (pauseseq) yield return timepause;
                }

                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);

                if (pauseseq) yield return timepause;

                blocks = blocks.Except(connectors)
                    .Except(connectorblocks)
                    .Except(landinggears)
                    .Except(landinggearblocks)
                    .Except(tankblocks)
                    .Except(taggedbats)
                    .Except(normalbats)
                    .Except(batteriesblocks) //backupbats
                    .Except(cruiseThr)
                    .Except(normalThrusters)
                    .Except(ccontrollerblocks)
                    .Except(controllerblocks)
                    .Except(vtrotors)
                    .Except(vtthrusters)
                    .Except(screens)
                    .Except(abandonedrotors)
                    .Except(abandonedthrusters)
                    .ToList();
                if (pauseseq) yield return timepause;

                foreach (IMyTerminalBlock b in blocks)
                {
                    if (GridTerminalSystem.CanAccess(b))
                    {
                        bool island = b is IMyLandingGear;
                        bool iscon = b is IMyShipConnector;
                        bool samegrid = FilterThis(b);
                        bool hastag = HasTag(b);
                        bool xor = (samegrid || hastag);

                        if (b is IMyShipController)
                        {
                            controllerblocks.Add((IMyShipController)b);
                            controllers_input.Add(new ShipController((IMyShipController)b, this));
                        }
                        else if (b is IMyThrust && samegrid)
                        {
                            IMyThrust tr = (IMyThrust)b;
                            normalThrusters.Add((IMyThrust)b);
                            if (b.Orientation.Forward == mainController.TheBlock.Orientation.Forward) //changing
                            {
                                cruiseThr.Add((IMyThrust)b);
                                log.AppendNR("Added back thrust: " + b.CustomName);
                            }
                            if (!justCompiled && stockvalues) (b as IMyFunctionalBlock).Enabled = true;
                        }
                        else if (b is IMyTextPanel)
                        {
                            input_screens.Add((IMyTextPanel)b);
                        }
                        else if (iscon)
                        {
                            if (TagAll) AddTag(b);
                            bool cond1 = AutoAddGridConnectors && xor;
                            bool cond2 = !AutoAddGridConnectors && hastag;

                            bool cncond = cond1 || cond2;

                            connectorblocks.Add((IMyShipConnector)b);
                            if (cncond && !connectors.Contains(b)) connectors.Add((IMyShipConnector)b);
                        }
                        else if (island)
                        {
                            if (TagAll) AddTag(b);
                            bool cond3 = AutoAddGridLandingGears && xor;
                            bool cond4 = !AutoAddGridLandingGears && hastag;

                            bool lgcond = cond3 || cond4;

                            landinggearblocks.Add((IMyLandingGear)b);
                            if (lgcond && !landinggears.Contains(b)) landinggears.Add((IMyLandingGear)b);
                        }
                        else if (b is IMyGasTank && (hastag || TagAll || samegrid))
                        {
                            if (TagAll) AddTag(b);
                            tankblocks.Add((IMyGasTank)b);
                            if (hastag && stockvalues) (b as IMyGasTank).Stockpile = false;
                        }
                        else if (b is IMyBatteryBlock)
                        {
                            IMyBatteryBlock bat = (IMyBatteryBlock)b;

                            if (TagAll) AddTag(b);
                            if (justCompiled && (hastag || samegrid) && stockvalues) bat.ChargeMode = ChargeMode.Auto;

                            if (hastag) taggedbats.Add(bat);
                            else if (samegrid) normalbats.Add(bat);
                            else batteriesblocks.Add(bat);
                        }
                        else if (b is IMySoundBlock && hastag)
                        {
                            IMySoundBlock sb = (IMySoundBlock)b;
                            sb.LoopPeriod = 1;
                            sb.SelectedSound = "Alert 2";
                            soundblocks.Add(sb);
                        }
                    }
                    if (pauseseq) yield return timepause;
                }
                // TODO: Compare if blocks are equal, or make other quick way to gather correct blocks (DONE)

                while (!GetControllers.Doneloop)
                {
                    GetControllers.Run();
                    if (pauseseq) yield return timepause;
                }
                GetControllers.Doneloop = false;

                while (!GetScreen.Doneloop)
                {
                    GetScreen.Run();
                    if (pauseseq) yield return timepause;
                }
                GetScreen.Doneloop = false;

                LND(ref controllerblocks);
                if (pauseseq) yield return timepause;

                LND(ref vectorthrusters);
                if (pauseseq) yield return timepause;

                LND(ref normalThrusters);
                if (pauseseq) yield return timepause;

                LND(ref vtthrusters);
                if (pauseseq) yield return timepause;

                LND(ref vtrotors);
                if (pauseseq) yield return timepause;

                check = false;
                yield return timepause;
            }
        }


        public IEnumerable<double> GetBatStatsSeq()
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
                        yield return timepause;
                    }
                    inputs /= inputs != 0 ? batsseq.Count : 1;
                    outputs /= outputs != 0 ? batsseq.Count : 1;
                    percents *= percents != 0 ? (100 / batsseq.Count) : 1;

                    outputbatsseq = new List<double> { inputs, outputs, percents.Round(0) };
                    yield return timepause;
                }

                BatteryStats.Doneloop = true;
                yield return timepause;
            }
        }



        public IEnumerable<double> BlockManagerSeq()
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
                    yield return timepause;
                    continue;
                }

                if (batteries.Empty() && backupbatteries.Empty())
                {
                    List<IMyBatteryBlock> allbats = new List<IMyBatteryBlock>(taggedbats).Concat(normalbats).ToList();
                    if (parked) yield return timepause;
                    backupbatteries = allbats.FindAll(x => x.CustomName.Contains(BackupSubstring));
                    if (parked) yield return timepause;

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
                    yield return timepause;
                }

                List<IMyPowerProducer> pw = new List<IMyPowerProducer>();
                GridTerminalSystem.GetBlocksOfType(pw, x => !batteries.Contains(x) && !backupbatteries.Contains(x));
                yield return timepause;

                List<double> statsBBATS = new List<double>();
                List<double> statsPW = new List<double>();
                yield return timepause;

                if (!donescan || (BlockManager.Doneloop && parked))
                {
                    if (parked)
                    { //temporary fix
                        batsseq = new List<IMyTerminalBlock>(pw);
                        while (!BatteryStats.Doneloop)
                        {
                            BatteryStats.Run();
                            yield return timepause;
                        }
                        BatteryStats.Doneloop = false;

                        donescan = true;
                        statsPW = new List<double>(outputbatsseq);
                    }
                    yield return timepause;

                    batsseq = new List<IMyTerminalBlock>(backupbatteries);
                    while (!BatteryStats.Doneloop)
                    {
                        BatteryStats.Run();
                        yield return timepause;
                    }
                    BatteryStats.Doneloop = false;

                    statsBBATS = new List<double>(outputbatsseq);
                    yield return timepause;
                }

                bool lowbackupbat = !statsBBATS.Empty() && statsBBATS[2] < 2.5;
                bool comebackupbat = !statsBBATS.Empty() && statsBBATS[2] > 25;
                yield return timepause;

                bool charging = batteries.All(x => x.ChargeMode == ChargeMode.Recharge);
                rechargecancelled = (statsPW.Empty() && donescan) || (!statsPW.Empty() && statsPW[1] == 0) || lowbackupbat;
                bool notcharged = parked && parkedwithcn && donescan && ((!charging && !rechargecancelled) || (charging && rechargecancelled)) && !batteries.Empty();
                bool reassign = rechargecancelled && !statsPW.Empty() && statsPW[1] != 0 && comebackupbat;
                yield return timepause;

                if (!((!parked && parkedwithcn) || notcharged || reassign))
                {
                    changedruntime = EndBM(donescan, changedruntime);
                    yield return timepause;
                    continue;
                }

                foreach (IMyGasTank t in tankblocks)
                {
                    t.Stockpile = !rechargecancelled && parked && t.FilledRatio != 1;
                    yield return timepause;
                }

                if (parked && !rechargecancelled)
                { //If I don't do this the ship will shut off
                    foreach (IMyBatteryBlock b in backupbatteries)
                    {
                        b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Auto : ChargeMode.Recharge;
                        yield return timepause;
                    }
                    foreach (IMyBatteryBlock b in batteries)
                    {
                        b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Recharge : ChargeMode.Auto;
                        yield return timepause;
                    }
                }
                else if (!parked || rechargecancelled)
                {
                    foreach (IMyBatteryBlock b in batteries)
                    {
                        b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Recharge : ChargeMode.Auto;
                        yield return timepause;
                    }
                    foreach (IMyBatteryBlock b in backupbatteries)
                    {
                        b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Auto : ChargeMode.Recharge;
                        yield return timepause;
                    }
                }

                changedruntime = EndBM(donescan, changedruntime);
                yield return timepause;
            }
        }
    }
}
