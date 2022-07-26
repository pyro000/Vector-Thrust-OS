using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
		string Separator(string title = "", int len = 58)
		{
			int tl = title.Length;
			len = (len - tl) / 2;
			string res = new string('-', len);
			return res + title + res;
		}

		void Printer()
		{
			string cstr = mainController != null ? mainController.TheBlock.CustomName : "DEAD";
			if (ShowMetrics) screensb.GetSpinner(ref pc).Append($" {Runtime.LastRunTimeMs.Round(2)}ms ").GetSpinner(ref pc);
			screensb.Append(progressbar);

			screensb.Append("\n").GetSpinner(ref pc).Append(trueaccel).GetSpinner(ref pc);
			screensb.AppendLine($"\nCruise: {cruise}");
			if (normalThrusters.Count == 0) screensb.AppendLine($"Dampeners: {dampeners}");
			if (ShowMetrics)
			{
				screensb.AppendLine($"\nAM: {(accel / gravLength).Round(2)}g");
				screensb.AppendLine($"Active VectorThrusters: {vectorthrusters.Count}");
				screensb.AppendLine($"Main/Ref Cont: {cstr}");
			}
			echosb.GetSpinner(ref pc).Append(" VTos ").GetSpinner(ref pc);
			echosb.AppendLine($"\n\n--- Main ---");
			echosb.AppendLine(" >Remaining: " + _RuntimeTracker.tremaining);
			echosb.AppendLine(" >Greedy: " + greedy);
			echosb.AppendLine($" >Angle Objective: {totalVTThrprecision.Round(1)}%");
			echosb.AppendLine($" >Main/Reference Controller: {cstr}");
			echosb.AppendLine($" >Parked: {parkedcompletely}/{unparkedcompletely}");
			if (isstation) echosb.AppendLine("CAN'T FLY A STATION, RUNNING WITH LESS RUNTIME.");
		}

		void WriteOutput()
		{
			Echo(echosb.ToString());
			Write(screensb.ToString());
			echosb.Clear();
			screensb.Clear();
		}
		bool SkipFrameHandler(bool tagcheck, string argument)
		{
			bool notrun = argument.Equals("") && !cruise && !dampchanged;
			bool handlers = false;
			if (!isstation)
			{
				MainChecker.Run();//RUNS VARIOUS PROCESSES SEPARATED BY A TIMER
				if (notrun)
				{
					handlers = PerformanceHandler();
					handlers = ParkHandler() || handlers;
					handlers = VTThrHandler() || handlers;
				}
				else if (tagcheck) MainTag(argument);
			}
			if (error)
			{
				ShutDown();
				return true;
			}
			else if (isstation) return true;
			else if (handlers)
			{
				echosb.AppendLine("Required Force: ---N");
				echosb.AppendLine("Total Force: ---N\n");
				echosb = _RuntimeTracker.Append(echosb);
				echosb.AppendLine("--- Log ---");
				echosb.Append(log);
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
			Echo(log.ToString());
			Runtime.UpdateFrequency = UpdateFrequency.None;
		}
		void GenerateProgressBar(bool perf = false)
		{
			double percent = gearaccel / maxaccel;
			if (!perf)
			{
				progressbar.Clear();
				progressbar.ProgressBar(percent, 30);
			}
			trueaccel = $" ({(percent * totaleffectivethrust).Round(2)} {{m/s^2}}) ";
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

		void Write(params object[] obj)
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
		}

		void LND<T>(ref List<T> obj)
		{
			obj = obj.Distinct().ToList();
		}
		public bool FilterThis(IMyTerminalBlock block)
		{
			return block.CubeGrid == Me.CubeGrid;
		}

		public void StabilizeVectorThrusters(bool rotorlock = true)
		{
			if (vtthrusters.Empty() && vtrotors.Empty()) return;
			vtthrusters.ForEach(x => { x.Enabled = !rotorlock; x.ThrustOverridePercentage = 0; });
			vtrotors.ForEach(x => { x.Enabled = !rotorlock; x.TargetVelocityRPM = 0; x.RotorLock = rotorlock; });
		}

		ShipController FindACockpit()
		{
			foreach (ShipController cont in controlledControllers)
			{
				if (!cont.TheBlock.Closed && cont.TheBlock.IsWorking)
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
				foreach (IMyTerminalBlock block in blocks)
					RemoveTag(block);
				//block.CustomName = block.CustomName.Replace(oldTag, "").Trim();
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
			block.CustomName = block.CustomName.Replace(tag, "").Trim();
		}

		void Init()
		{
			log.AppendLine("Init() Start");
			Config();
			ManageTag();
			InitControllers();

			check = true;
			if (mainController != null)
			{
				myshipmass = mainController.TheBlock.CalculateShipMass();
				oldMass = myshipmass.BaseMass;
			}
			OneRunMainChecker();
			log.AppendLine("Init " + (error ? "Failed" : "Completed Sucessfully"));

			//return !error;
		}

		void InitControllers(List<IMyShipController> blocks = null) //New GetControllers(), only for using in init() purpose 
		{
			bool greedy = this.greedy || this.applyTags;// || this.removeTags;

			if (blocks == null)
			{
				blocks = new List<IMyShipController>();
				GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks);
			}

			List<ShipController> conts = new List<ShipController>();
			foreach (IMyShipController imy in blocks)
			{
				//vtcontrollers.Add(imy);
				controllerblocks.Add(imy);
				conts.Add(new ShipController(imy/*, this*/));
			}
			//return getControllers(conts);

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
				/*if (this.removeTags)
				{
					RemoveTag(s.TheBlock);
				}*/

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

			if (controlledControllers.Count == 0 /*&& usableControllers.Count == 0*/)
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

		bool VTThrHandler()
		{
			bool nograv = wgv == 0;
			bool preventer = thrustontimer > 0.1 && !nograv && !parked && sv != 0;
			bool unparking = !parked && alreadyparked;
			bool partiallyparked = parked && alreadyparked;
			bool standby = (nograv || partiallyparked) && totalVTThrprecision.Round(1) == 100 && setTOV && !thrustOn && mvin == 0;

			//screensb.AppendLine($"S: {standby}/{parkedcompletely}=>{rotorsstopped}/{unparking}");
			//screensb.AppendLine($"CONDS: {totalVTThrprecision.Round(1) == 100}/{setTOV}/{!thrustOn}/{mvin == 0}");
			if (standby || parkedcompletely)
			{
				echosb.AppendLine("\nEverything stopped, performance mode.\n");

				bool cond1 = vtthrusters.All(x => x.Enabled == false && x.ThrustOverridePercentage == 0);
				bool cond2 = vtrotors.All(x => x.Enabled == false && x.TargetVelocityRPM == 0 && x.RotorLock == true);
				rotorsstopped = cond1 && cond2;

				if (!rotorsstopped) StabilizeVectorThrusters();
				return true;
			}
			else if ((rotorsstopped && setTOV) || unparking || preventer) // IT NEEDS TO BE UNPARKING INSTEAD OF TOTALLY UNPARKED
			{
				setTOV = rotorsstopped = false;
				StabilizeVectorThrusters(false);
				if (preventer) thrustontimer = 0;
			}
			return rotorsstopped;
		}
		bool PerformanceHandler()
		{
			if (SkipFrames > 0)
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

		bool ParkHandler()
		{
			if (connectors.Count == 0 && landinggears.Count == 0) return false;

			if (unparkedcompletely)
			{
				parkedwithcn = connectors.Any(x => x.Status == MyShipConnectorStatus.Connected);
				parked = landinggears.Any(x => x.IsLocked) || parkedwithcn;
			}
			else
			{ //Modifying
				parked = landinggears.Any(x => x.IsLocked) || connectors.Any(x => x.Status == MyShipConnectorStatus.Connected);
			}
			unparkedcompletely = !parked && !alreadyparked;
			if (unparkedcompletely) return false;

			bool setvector = parked && alreadyparked && setTOV;
			bool gotvector = totalVTThrprecision.Round(1) == 100;
			parkedcompletely = setvector && gotvector;

			bool pendingrotation = setvector && !gotvector;
			bool parking = parked && !alreadyparked;
			bool unparking = !parked && alreadyparked;

			if (parking || (unparking && BlockManager.Doneloop)) ResetParkingSeq();
			if (parkedcompletely || (unparking && !BlockManager.Doneloop))
			{
				if (parkedcompletely && BlockManager.Doneloop) screensb.AppendLine("- PARKED -");
				else if (parkedcompletely && !BlockManager.Doneloop) screensb.GetSpinner(ref pc).Append(" ASSIGNING ").GetSpinner(ref pc, after: "\n");
				else screensb.GetSpinner(ref pc).Append(" UNPARKING ").GetSpinner(ref pc, after: "\n");
				BlockManager.Run();
			}
			if (pendingrotation) screensb.GetSpinner(ref pc).Append(" PARKING ").GetSpinner(ref pc, after: "\n");

			return parkedcompletely;
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

		double GetAcceleration(double gravity, double exp = 0)
		{
			// look through boosts, applies acceleration of first one found
			if (useBoosts && this.controlModule)
			{
				for (int i = 0; i < this.boosts.Length; i++)
				{
					if (this.CMinputs.ContainsKey(this.boosts[i].button))
					{
						return this.boosts[i].accel * gravity * defaultAccel;
					}
				}
			}

			double accelexpaval = exp == 0 ? accelExponent_A : exp;
			double gravtdefac = gravity * defaultAccel;

			//getting max & gear accel
			gearaccel = Math.Pow(accelBase, accelExponent + Accelerations[gear]) * gravtdefac;
			maxaccel = Math.Pow(accelBase, accelExponent + Accelerations[Accelerations.Length - 1]) * gravtdefac;
			//none found or boosts not enabled, go for normal accel
			return Math.Pow(accelBase, accelExponent + accelexpaval) * gravtdefac;
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
				if (this.CMinputs.ContainsKey(dampenersButton) && !dampenersIsPressed)
				{//inertia dampener key
					dampeners = !dampeners;//toggle
					dampenersIsPressed = true;
				}
				if (!this.CMinputs.ContainsKey(dampenersButton))
				{
					dampenersIsPressed = false;
				}


				if (this.CMinputs.ContainsKey(cruiseButton) && !cruiseIsPressed)
				{//cruise key
					cruise = !cruise;//toggle
					cruiseIsPressed = true;
				}
				if (!this.CMinputs.ContainsKey(cruiseButton))
				{
					cruiseIsPressed = false;
				}

				if (this.CMinputs.ContainsKey(raiseAccel) && !plusIsPressed)
				{//throttle up
					accelExponent++;
					plusIsPressed = true;
				}
				if (!this.CMinputs.ContainsKey(raiseAccel))
				{ //increase target acceleration
					plusIsPressed = false;
				}

				if (this.CMinputs.ContainsKey(lowerAccel) && !minusIsPressed)
				{//throttle down
					accelExponent--;
					minusIsPressed = true;
				}
				if (!this.CMinputs.ContainsKey(lowerAccel))
				{ //lower target acceleration
					minusIsPressed = false;
				}

				if (this.CMinputs.ContainsKey(resetAccel))
				{ //default target acceleration
					accelExponent = 0;
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
				else if (arg.Contains(raiseAccelArg))
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
				}
				else if (arg.Contains(gearArg))
				{
					if (gear == Accelerations.Length - 1) gear = 0;
					else gear++;
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
					Runtime.UpdateFrequency = UpdateFrequency.Update100;
					return;
				}
				else if (isstation)
				{
					isstation = false;
					Runtime.UpdateFrequency = update_frequency;
				}
				if (this.oldMass == bm) return; //modifying variables here may cause to the handler to restart every single time

				this.oldMass = bm; //else:
			}
			OneRunMainChecker(false);
			if (!justCompiled) GenerateProgressBar(true);
		}

		void EndBM(bool scanned)
		{
			if (scanned && parked)
			{
				BlockManager.Doneloop = true;
				if (PerformanceWhilePark && gravLength == 0 && Runtime.UpdateFrequency != UpdateFrequency.Update100) Runtime.UpdateFrequency = UpdateFrequency.Update100;
				else if ((!PerformanceWhilePark || gravLength > 0) && Runtime.UpdateFrequency != UpdateFrequency.Update10) Runtime.UpdateFrequency = UpdateFrequency.Update10;
			}
			else if (!parked)
			{
				parkedwithcn = alreadyparked = false;
			}
		}
    }
}
