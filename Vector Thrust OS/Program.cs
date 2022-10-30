using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string dampenersArg = "dampeners";
        const string cruiseArg = "cruise";
        const string gearArg = "gear";
        const string applyTagsArg = "applytags";
        const string applyTagsAllArg = "applytagsall";
        const string removeTagsArg = "removetags";

        //readonly RuntimeTracker _RuntimeTracker;
        readonly SimpleTimerSM BlockManager;
        readonly SimpleTimerSM BatteryStats;

        readonly double timeperframe = 1.0 / 60.0;
        Vector3D desiredVec = new Vector3D();
        bool dampchanged = false;

        bool parked = false;
        bool alreadyparked = false;
        bool cruisedNT = false;
        bool setTOV = false;
        bool TagAll = false;
        bool error = false;
        bool oldDampeners = false;
        bool isstation = false;
        double wgv = 0;
        double mvin = 0;
        double accel = 0;
        double maxaccel = 0;
        float gravLength = 0;
        string oldTag = "";
        readonly StringBuilder echosb = new StringBuilder();
        readonly StringBuilder screensb = new StringBuilder();
        readonly StringBuilder log = new StringBuilder();
        //long pc = 0;
        MyShipMass myshipmass;

        bool cruise = false;
        bool dampeners = true;
        string textSurfaceKeyword = "VT:";
        string LCDName = "VTLCD";
        const float defaultAccel = 1f;
        const float accelBase = 1.5f;//accel = defaultAccel * g * base^exponent
                                     // your +, - and 0 keys increment, decrement and reset the exponent respectively
                                     // this means increasing the base will increase the amount your + and - change target acceleration
        const float dampenersModifier = 0.1f; // multiplier for dampeners, higher is stronger dampeners		 
        const float zeroGAcceleration = 9.81f; // default acceleration in situations with 0 (or low) gravity				 
        const float gravCutoff = 0.1f * zeroGAcceleration;  // if gravity becomes less than this, zeroGAcceleration will kick in (I think it's deprecated)
        
        //readonly UpdateFrequency update_frequency = UpdateFrequency.Update1;
        // choose wether you want the script to											 
        // update once every frame, once every 10 frames, or once every 100 frames (Recommended not modifying it)

        bool controlModule = true;
        const string dampenersButton = "c.damping"; //Z
        const string cruiseButton = "c.cubesizemode"; //R
        const string gearButton = "c.sprint"; //Shift
        const string allowparkButton = "c.thrusts";//X 
        //"c.stationrotation"; //B
        bool dampenersIsPressed = false;
        bool cruiseIsPressed = false;
        bool gearIsPressed = false;
        bool allowparkIsPressed = false;

        double rawgearaccel = 0;
        double tthrust = 0;
        double len = 0;
        double totaleffectivethrust = 0;

        readonly StringBuilder surfaceProviderErrorStr = new StringBuilder();
        double totalVTThrprecision = 0;
        bool rotorsstopped = false;

        ShipController mainController = null;
        List<ShipController> controllers = new List<ShipController>();
        List<IMyShipController> controllerblocks = new List<IMyShipController>();
        readonly List<IMyShipController> ccontrollerblocks = new List<IMyShipController>();
        readonly List<ShipController> controlledControllers = new List<ShipController>();
        List<VectorThrust> vectorthrusters = new List<VectorThrust>();
        List<IMyThrust> normalThrusters = new List<IMyThrust>();
        List<IMyTextPanel> screens = new List<IMyTextPanel>();
        readonly List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        readonly List<IMyLandingGear> landinggears = new List<IMyLandingGear>();
        readonly List<IMyGasTank> tankblocks = new List<IMyGasTank>();
        readonly List<IMyTerminalBlock> cruiseThr = new List<IMyTerminalBlock>();
        readonly List<List<VectorThrust>> VTThrGroups = new List<List<VectorThrust>>();
        public List<IMyTextSurface> surfaces = new List<IMyTextSurface>();

        List<IMyThrust> vtthrusters = new List<IMyThrust>();
        List<IMyMotorStator> vtrotors = new List<IMyMotorStator>();

        readonly List<IMyBatteryBlock> taggedbats = new List<IMyBatteryBlock>();
        readonly List<IMyBatteryBlock> normalbats = new List<IMyBatteryBlock>();

        readonly List<IMySoundBlock> soundblocks = new List<IMySoundBlock>();
        readonly List<IMyShipConnector> connectorblocks = new List<IMyShipConnector>();
        readonly List<IMyLandingGear> landinggearblocks = new List<IMyLandingGear>();
        readonly List<IMyBatteryBlock> batteriesblocks = new List<IMyBatteryBlock>();

        List<IMyThrust> thrusters_input = new List<IMyThrust>();
        List<IMyMotorStator> rotors_input = new List<IMyMotorStator>();
        readonly List<ShipController> controllers_input = new List<ShipController>();
        readonly List<IMyTextPanel> input_screens = new List<IMyTextPanel>();
        readonly List<IMyMotorStator> abandonedrotors = new List<IMyMotorStator>();
        readonly List<IMyThrust> abandonedthrusters = new List<IMyThrust>();

        List<IMyTerminalBlock> batsseq = new List<IMyTerminalBlock>();
        List<double> outputbatsseq = new List<double>();

        bool pauseseq = false;
        bool check = true;

        Vector3D lastvelocity = Vector3D.Zero;
        readonly int updatespersecond = 60;
        float accel_aux = 0;
        Vector3D shipVelocity = Vector3D.Zero;
        double sv = 0;
        bool justCompiled = true;
        string tag = "|VT|";

        bool applyTags = false;
        bool greedy = true;
        float lastGrav = 0;
        bool thrustOn = true;
        Dictionary<string, object> CMinputs = null;
        bool rechargecancelled = false;
        bool parkedwithcn = false;

        float oldMass = 0;
        int frame = 0;
        double force = 0;
        double tgotTOV = 0;

        bool cruisebyarg = false;
        bool trulyparked = false;
        bool parkavailable = false;
        bool almostbraked = false;
        bool unparkedcompletely = true;
        bool parkedcompletely = false;

        readonly WhipsHorizon WH;
        Vector3D worldGrav = Vector3D.Zero;
        readonly Tracker tracker;

        readonly SimpleTimerSM MainChecker;
        readonly SimpleTimerSM GetScreen;
        readonly SimpleTimerSM GetControllers;
        readonly SimpleTimerSM GetVectorThrusters;
        readonly SimpleTimerSM CheckParkBlocks;
        //readonly SimpleTimerSM GetEffectiveThrust;

        DebugAPI Debug;
        public Program()
        {
            Debug = new DebugAPI(this);

            log.AppendLine("Program() Start");

            Load();
            //_RuntimeTracker = new RuntimeTracker(this, 60, 0.005);
            tracker = new Tracker(this, 100, 500);

            BlockManager = new SimpleTimerSM(this, BlockManagerSeq(), true);
            BatteryStats = new SimpleTimerSM(this, GetBatStatsSeq(), true);
            MainChecker = new SimpleTimerSM(this, CheckVectorThrustersSeq(), true);
            GetScreen = new SimpleTimerSM(this, GetScreensSeq(), true);
            GetControllers = new SimpleTimerSM(this, GetControllersSeq(), true);
            GetVectorThrusters = new SimpleTimerSM(this, GetVectorThrustersSeq(), true);
            CheckParkBlocks = new SimpleTimerSM(this, CheckParkBlocksSeq(), true);
            //GetEffectiveThrust = new SimpleTimerSM(this, GetEffectiveThrustSeq(), true);
            WH = new WhipsHorizon(surfaces, this);
            Init();

            if (!error) Runtime.UpdateFrequency = UpdateFrequency.Update1;
            Echo(log.ToString());
            log.AppendLine("--VTOS Started--");
        }

        public void Save()
        {
            string save = string.Join(";", string.Join(":", tag, greedy), allowpark, gear);
            Storage = save; //saving the old tag and greedy to prevent recompile or script update confusion
        }

        public void Load() {
            string[] saved = Storage.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            if (saved.Length >= 2)
            {
                allowpark = bool.Parse(saved[1]); //Gets if the user set in park mode when recompile or reload, prevents accidents

                string[] stg = saved[0].Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                if (stg.Length == 2)
                {
                    oldTag = stg[0]; //loading tag
                    greedy = bool.Parse(stg[1]); //loading greedy
                }

                if (saved.Length == 3)
                {
                    int g = int.Parse(saved[2]);
                    if (Accelerations.Count - 1 >= g)
                        gear = g;
                }
            }
        }

        //double avg = 0;
        //readonly EMA avgruntime = new EMA(5000);
        //double maxruntime = 0;
        //bool canprint = false;
        

        public void Main(string argument)
        {
            argument = argument.ToLower();
            tracker.Process();
            Printer();

            //0.005 0.008

            Config();

            //0.006 0.011
            

            if (EnDebugAPI) Debug.RemoveDraw();

            // ========== STARTUP ==========
            //_RuntimeTracker.AddRuntime();

            /*if (_RuntimeTracker.configtrigger)
            {
                _RuntimeTracker.configtrigger = false;
                Config();
                ManageTag();
            }*/

            // GETTING ONLY NECESARY INFORMATION TO THE SCRIPT
            if (!parkedcompletely || argument.Length > 0 || trulyparked)
            {
                MyShipVelocities shipVelocities = mainController.TheBlock.GetShipVelocities();
                shipVelocity = shipVelocities.LinearVelocity;
                sv = shipVelocity.Length();

                if (!parkedcompletely || argument.Length > 0) { 
                    bool damp = mainController.TheBlock.DampenersOverride;
                    dampchanged = damp != oldDampeners;
                    oldDampeners = damp;

                    desiredVec = GetMovementInput(argument, parked);
                    mvin = desiredVec.Length();

                    almostbraked = mvin == 0 && sv < velprecisionmode;
                    ThrustOnHandler();
                }
            }

            //0.01 0.016

            // END NECESARY INFORMATION

            // SKIPFRAME AND PERFORMANCE HANDLER: handler to skip frames, it passes out if the player doesn't parse any command or do something relevant.
            /*if (!justCompiled)*/ CheckWeight(); //Block Updater must-have

            
            //0.011 0.016

            if (SkipFrameHandler(argument)) return;

            //0.016 0.019-0.025 Unparked

            // END SKIPFRAME

            // ========== PHYSICS ==========
            //TODO: SEE IF I CAN SPLIT AT LEAST SOME OF THE STEPS BY SEQUENCES
            float shipMass = myshipmass.PhysicalMass;
            worldGrav = mainController.TheBlock.GetNaturalGravity();
            gravLength = (float)worldGrav.Length();

            bool gravChanged = Math.Abs(lastGrav - gravLength) > 0.05f;
            /*foreach (VectorThrust n in vectorthrusters) {
                if (n != null && CheckRotor(n.rotor.TheBlock) && !n.thrusters.Empty() && (gravChanged || !n.ValidateThrusters())) { 
                    n.DetectThrustDirection(); 
                }
            }*/
            wgv = lastGrav = gravLength;

            // setup gravity
            if (gravLength < gravCutoff) gravLength = zeroGAcceleration;

            // f=ma
            Vector3D shipWeight = shipMass * worldGrav;

            if (dampeners)
            {
                Vector3D dampVec = Vector3D.Zero;
                if (desiredVec != Vector3D.Zero)
                {
                    // cancel movement opposite to desired movement direction
                    if (Extensions.Dot(desiredVec, shipVelocity) < 0)
                    {
                        //if you want to go oppisite to velocity
                        dampVec += VectorMath.Projection(shipVelocity,/*.Project(*/desiredVec.Normalized());
                    }
                    // cancel sideways movement
                    dampVec += VectorMath.Rejection(shipVelocity, desiredVec.Normalized());//shipVelocity.Reject(desiredVec.Normalized());
                }
                else
                {
                    dampVec += shipVelocity;
                }


                if (cruise)
                {
                    if (cruiseThr.Count > 0 && !cruisedNT)
                    {
                        cruisedNT = true;
                        foreach (IMyFunctionalBlock b in cruiseThr) b.Enabled = false;
                    }

                    foreach (ShipController cont in controlledControllers)
                    {
                        if ((OnlyMain() && cont != mainController) || !cont.TheBlock.IsUnderControl) continue;

                        if (Extensions.Dot(dampVec, cont.TheBlock.WorldMatrix.Forward) > 0 || cruisePlane)
                        { // only front, or front+back if cruisePlane is activated
                            dampVec -= VectorMath.Projection( dampVec,/*.Project(*/cont.TheBlock.WorldMatrix.Forward);
                        }

                        if (cruisePlane)
                        {
                            shipWeight -= VectorMath.Projection( shipWeight, /*.Project(*/cont.TheBlock.WorldMatrix.Forward);
                        }
                    }
                }
                else if (!cruise && cruisedNT)
                {
                    cruisedNT = false;
                    cruiseThr.ForEach(b => (b as IMyFunctionalBlock).Enabled = true);
                    
                }

                cruise = justCompiled || (wgv != 0 && sv == 0) || trulyparked || cruiseThr.Empty() || cruisebyarg || parked || alreadyparked || BlockManager.Doneloop ? cruise : cruiseThr.All(x => !(x as IMyFunctionalBlock).Enabled); //New cruise toggle mode

                desiredVec -= dampVec * dampenersModifier;
            }
            // f=ma

            //0.015 0.023
            GetAcceleration();
            
            lastvelocity = shipVelocity;
            desiredVec *= shipMass * (float)accel;

            // point thrust in opposite direction, add weight. this is force, not acceleration
            Vector3D requiredVec = -desiredVec + shipWeight;

            // remove thrust done by normal thrusters
            Vector3D nthrthrust = Vector3D.Zero;
            
            foreach (IMyThrust t in normalThrusters)
            {
                nthrthrust += t.WorldMatrix.Backward * t.CurrentThrust;
            }
            double thrustbynthr = nthrthrust.Length();
            rawgearaccel += thrustbynthr;
            requiredVec += nthrthrust;

            len = requiredVec.Length();
            //if (CanPrint()) { echosb.AppendLine($"Required Force: {len.Round(0)}N"); }
            // ========== END OF PHYSICS ==========

            //0.019-0.021 0.026-0.036

            // ========== DISTRIBUTE THE FORCE EVENLY BETWEEN NACELLES ==========

            ParkVector(ref requiredVec, shipMass);

            //double total = 0;
            totalVTThrprecision = 0;
            totaleffectivethrust = 0;
            tthrust = 0;
            rawgearaccel = 0;
            //same

            // NEW THRUSTER ALIGNMENT AND VECTOR ASSIGNMENTº SYSTEM
            int gc = VTThrGroups.Count;
            for (int i = 0; i < gc; i++)
            {
                List<VectorThrust> g = VTThrGroups[i];
                int tc = g.Count;
                if (tc <= 0) continue;
                int ni = i + 1;

                //Print($"Group {ni}/{gc}");

                int c = g.Count(x => x.totalEffectiveThrust > 0).Clamp(1, tc);
                // This for some reason fixes a crash on station grids on compile, also from parking

                Vector3D vectemp = VectorMath.Rejection(requiredVec, g[0].rotor.TheBlock.WorldMatrix.Up);
                Vector3D nextvectemp = Vector3D.Zero;

                if (g[0].rotor.isHinge && Vector3D.Dot(vectemp, g[0].rotor.TheBlock.WorldMatrix.Left) <= 0)
                { //is pointed left
                    vectemp = VectorMath.Rejection(vectemp, g[0].rotor.TheBlock.WorldMatrix.Right);
                }

                //0.018-0.023.0.076 0.026-0.032-0.06

                if (ni < gc && tets[ni] > 0)
                {
                    VectorThrust nextvt = VTThrGroups[ni][0];

                    nextvectemp = VectorMath.Rejection(vectemp, nextvt.rotor.TheBlock.WorldMatrix.Up)/* * 0.15*/;

                    if (nextvt.rotor.isHinge) {
                        bool nisPointedLeft = Vector3D.Dot(nextvectemp, nextvt.rotor.TheBlock.WorldMatrix.Left) > 0;
                        if (!nisPointedLeft) nextvectemp = VectorMath.Rejection(nextvectemp, nextvt.rotor.TheBlock.WorldMatrix.Right);
                    }

                    nextvectemp *= 0.15; //Gifting 15% of the vector to the next group
                    nextvectemp = nextvectemp.Clamp(0.01, tets[ni]); //limiting the vector's with the previous totaleffectivethrust

                    //Print($"n: {nextvectemp.Length().Round(2)}");
                    vectemp -= nextvectemp;
                }

                double temp = (vectemp / c).Length();
                tets[i] = 0;
                Vector3D assignedvec = (vectemp - nextvectemp).Normalized();

                
                //0.018.0.024.0.033 0.029-0.031-0.111

                for (int j = 0; j < tc; j++)
                {
                    VectorThrust vt = g[j];

                    //Print($"{vt.rotor.CName}");

                    if (!CheckRotor(vt.rotor.TheBlock)) continue;

                    if (!vt.thrusters.Empty() && (gravChanged || !vt.ValidateThrusters()))
                    {
                        vt.DetectThrustDirection();
                    }

                    //0.028-0.038 0.033-0.045
                    

                    /*if ((thrustOn && !parked) || (!thrustOn && !vt.activeThrusters.Empty()))*/
                    double tet = vt.CalcTotalEffectiveThrust();
                    //double tet = vt.totalEffectiveThrust;
                    tets[i] += tet;

                    //0.029-0.033 0.038-0.045
                    //Print($"{vt.rotorz}");
                    //vt.requiredVec = vt.rotor.isHinge ? isPointedLeft ? /*requiredVec*/vt.requiredVec : VectorMath.Rejection(/*requiredVec*/vt.requiredVec, vt.rotor.TheBlock.WorldMatrix.Right) : /*requiredVec*/vt.requiredVec;
                    //vt.requiredVec = (VectorMath.Rejection(vt.requiredVec, vt.rotor.TheBlock.WorldMatrix.Up)-nextvectemp).Normalized() * temp.Clamp(0.01, tet);
                    //if (tet == 0) vt.requiredVec = vectemp.Normalized();
                    //Debug.DrawLine(vt.rotor.TheBlock.GetPosition(), vt.rotor.TheBlock.GetPosition() + vt.requiredVec, Color.LightGreen, onTop: true);

                    vt.requiredVec = assignedvec;

                    /*if (vt.rotor.isHinge && Vector3D.Dot(vt.requiredVec, vt.rotor.TheBlock.WorldMatrix.Left) <= 0) { //is pointed left
                        vt.requiredVec = VectorMath.Rejection(vt.requiredVec, vt.rotor.TheBlock.WorldMatrix.Right);
                    }*/
                    //bool isPointedLeft = Vector3D.Dot(/*requiredVec*/vt.requiredVec, vt.rotor.TheBlock.WorldMatrix.Left) > 0; //this lets me use hinges, TODO (Completed): Determine correct hinge direction
                    //vt.requiredVec = vt.rotor.isHinge && !isPointedLeft ? VectorMath.Rejection(vt.requiredVec, vt.rotor.TheBlock.WorldMatrix.Right) : vt.requiredVec;

                    vt.requiredVec *= tet > 0 ? temp.Clamp(1, tet) : 1;

                    //0.029-0.033-0.042 0.038-0.042

                    vt.Go();

                    //0.095-0.193 0.094-0.117

                    requiredVec -= vt.requiredVec;
                    totaleffectivethrust += tet * 1.595;
                    rawgearaccel += tet;
                    totalVTThrprecision += vt.rotor.LastAngleCos;
                    //total += vt.requiredVec.Length();
                }
            }

            //0.097-0.124 0.095-0.125

            //WO : 0.071 - 121
            totalVTThrprecision /= /*j*/vectorthrusters.Count;
            
            tthrust += thrustbynthr;
            tthrust /= myshipmass.TotalMass;
            totaleffectivethrust += thrustbynthr; //DON'T DELETE THIS, THIS SOLVES THE THRUST POINTING TO THE OPPOSITE

            //if (CanPrint())
            //{
                //echosb.AppendLine($"Total Force: {total.Round(0)}N\n");
                //echosb = _RuntimeTracker.Append(echosb);

                /*if (ShowMetrics)
                {
                    echosb.AppendLine("--- Log ---");
                    echosb.Append(log);
                }*/

                //log.Append(surfaceProviderErrorStr);
            //}

            justCompiled = false;
            //_RuntimeTracker.AddInstructions();
            // AVG RUNTIME: 0.09 - 0.10 AVG INSTRUCTIONS: 228
            // WO : 0.079 213

            // ========== END OF MAIN ==========
        }


        //List<double> minthrusts = new List<double> { 0, 0, 0};
        List<double> tets = new List<double>();
    }
}
