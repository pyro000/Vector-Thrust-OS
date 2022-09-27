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

        readonly RuntimeTracker _RuntimeTracker;
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
        float gravLength = 0;
        string oldTag = "";
        StringBuilder echosb = new StringBuilder();
        readonly StringBuilder screensb = new StringBuilder();
        readonly StringBuilder log = new StringBuilder();
        long pc = 0;
        MyShipMass myshipmass;


        readonly SimpleTimerSM MainChecker;
        readonly SimpleTimerSM GetScreen;
        readonly SimpleTimerSM GetControllers;
        readonly SimpleTimerSM GetVectorThrusters;
        readonly SimpleTimerSM CheckParkBlocks;

        double maxaccel = 0;

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
        
        readonly UpdateFrequency update_frequency = UpdateFrequency.Update1;
        // choose wether you want the script to											 
        // update once every frame, once every 10 frames, or once every 100 frames (Recommended not modifying it)

        // Control Module params... this can always be true, but it's deprecated
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


        double totaleffectivethrust = 0;
        double totalaccel = 0;
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

        bool unparkedcompletely = true;
        bool parkedcompletely = false;

        readonly WhipsHorizon WH;
        Vector3D worldGrav = Vector3D.Zero;

        public Program()
        {
            log.AppendLine("Program() Start");

            Load();
            _RuntimeTracker = new RuntimeTracker(this, 60, 0.005);
            BlockManager = new SimpleTimerSM(this, BlockManagerSeq(), true);
            BatteryStats = new SimpleTimerSM(this, GetBatStatsSeq(), true);
            MainChecker = new SimpleTimerSM(this, CheckVectorThrustersSeq(), true);
            GetScreen = new SimpleTimerSM(this, GetScreensSeq(), true);
            GetControllers = new SimpleTimerSM(this, GetControllersSeq(), true);
            GetVectorThrusters = new SimpleTimerSM(this, GetVectorThrustersSeq(), true);
            CheckParkBlocks = new SimpleTimerSM(this, CheckParkBlocksSeq(), true);
            WH = new WhipsHorizon(surfaces, this);
            Init();
            
            if (!error) Runtime.UpdateFrequency = update_frequency;
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
                    if (Accelerations.Length - 1 >= g)
                        gear = g;
                }
            }
        }

        bool trulyparked = false;
        bool parkavailable = false;
        Vector3D lastvelocity = Vector3D.Zero;
        readonly int updatespersecond = 60;
        float accel_aux = 0;
        bool almostbraked = false;

        public void Main(string argument)
        {
            // ========== STARTUP ==========
            _RuntimeTracker.AddRuntime();

            argument = argument.ToLower();

            if (_RuntimeTracker.configtrigger)
            {
                _RuntimeTracker.configtrigger = false;
                Config();
                ManageTag();
            }

            // GETTING ONLY NECESARY INFORMATION TO THE SCRIPT
            if (!parkedcompletely || argument.Length > 0 || trulyparked)
            {

                MyShipVelocities shipVelocities = mainController.TheBlock.GetShipVelocities();
                shipVelocity = shipVelocities.LinearVelocity;
                sv = shipVelocity.Length();

                if (!parkedcompletely || argument.Length > 0) { 
                    bool damp = mainController.TheBlock.DampenersOverride;
                    dampchanged = damp != oldDampeners;
                    oldDampeners = mainController.TheBlock.DampenersOverride;

                    desiredVec = GetMovementInput(argument, parked);
                    mvin = desiredVec.Length();

                    almostbraked = mvin == 0 && sv < velprecisionmode;
                    ThrustOnHandler();
                }
            }
            // END NECESARY INFORMATION

            //_RuntimeTracker.RegisterAction("Action");

            // AVG RUNTIME: 0.02 AVG INSTRUCTIONS: 24

            //START OUTPUT PRINTING
            Printer();
            //END PRINTER PART 1

            // AVG RUNTIME: 0.032 AVG INSTRUCTIONS: 34

            // SKIPFRAME AND PERFORMANCE HANDLER: handler to skip frames, it passes out if the player doesn't parse any command or do something relevant.
            if (!justCompiled) CheckWeight(); //Block Updater must-have

            if (SkipFrameHandler(argument)) return;
            // END SKIPFRAME

            // AVG RUNTIME: 0.04 AVG INSTRUCTIONS: 50 - min: 47
            // TEST: 0.116 216
            // TEST1 sin printer y routine: 0.09 179

            // ========== PHYSICS ==========
            //TODO: SEE IF I CAN SPLIT AT LEAST SOME OF THE STEPS BY SEQUENCES
            float shipMass = myshipmass.PhysicalMass;
            worldGrav = mainController.TheBlock.GetNaturalGravity();
            gravLength = (float)worldGrav.Length();

            bool gravChanged = Math.Abs(lastGrav - gravLength) > 0.05f;
            foreach (VectorThrust n in vectorthrusters) {
                if ((!n.ValidateThrusters() || gravChanged) && n != null && !n.thrusters.Empty() && CheckRotor(n.rotor.TheBlock)) { 
                    n.DetectThrustDirection(); 
                }
            }
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
                        dampVec += shipVelocity.Project(desiredVec.Normalized());
                    }
                    // cancel sideways movement
                    dampVec += shipVelocity.Reject(desiredVec.Normalized());
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
                            dampVec -= dampVec.Project(cont.TheBlock.WorldMatrix.Forward);
                        }

                        if (cruisePlane)
                        {
                            shipWeight -= shipWeight.Project(cont.TheBlock.WorldMatrix.Forward);
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
            GetAcceleration();
            accel_aux = !thrustOn || almostbraked ? (float)gearaccel.Round(2) : (float)((shipVelocity - lastvelocity) * updatespersecond).Length();
            lastvelocity = shipVelocity;
            desiredVec *= shipMass * (float)accel;

            // point thrust in opposite direction, add weight. this is force, not acceleration
            Vector3D requiredVec = -desiredVec + shipWeight;
            double thrustbynthr = 0;
           
            // remove thrust done by normal thrusters
            foreach (IMyThrust t in normalThrusters)
            {
                requiredVec += t.WorldMatrix.Backward * t.CurrentThrust;
                thrustbynthr += t.CurrentThrust;
            }

            len = requiredVec.Length();
            if (CanPrint()) { echosb.AppendLine($"Required Force: {len.Round(0)}N"); }
            // ========== END OF PHYSICS ==========


            // ========== DISTRIBUTE THE FORCE EVENLY BETWEEN NACELLES ==========

            if (!thrustOn)
            {// Zero G
                ParkVector(ref requiredVec, shipMass);
                setTOV = true;
            }

            // AVG RUNTIME: 0.048 AVG INSTRUCTIONS: 88

            // Correct for misaligned VTS
            Vector3D asdf = Vector3D.Zero;
            // 1
            foreach (List<VectorThrust> g in VTThrGroups)
            {
                if (!g.Empty())
                {
                    g[0].requiredVec = requiredVec.Reject(g[0].rotor.TheBlock.WorldMatrix.Up);
                    asdf += g[0].requiredVec;
                }
            }
            // 2
            asdf -= requiredVec;
            // 3

            foreach (List<VectorThrust> g in VTThrGroups)
            {
                if (!g.Empty())
                {
                    g[0].requiredVec -= asdf;
                }
            }
            // 4
            asdf /= VTThrGroups.Count;
            // 5
            foreach (List<VectorThrust> g in VTThrGroups)
            {
                if (!g.Empty())
                {
                    g[0].requiredVec += asdf;
                }
            }

            // apply first VT settings to rest in each group
            double total = 0;
            int j = 0;
            totalVTThrprecision = 0;


            if (ShowMetrics)
            {
                info = new StringBuilder($"{Separator("[Metrics]")}\n");
                edge = Separator();
                info.Append("| Axis |=> | VTLength | MaxRPM | Far% |\n")
                    .Append(edge);
            }

            totaleffectivethrust = 0;
            tthrust = 0;

            // AVG RUNTIME: 0.051- AVG INSTRUCTIONS: 109 - min: 106

            foreach (List<VectorThrust> g in VTThrGroups)
            {
                if (g.Empty()) continue;

                Vector3D req = g[0].requiredVec / g.Count;
                bool adjustmode = g.Any(x => x.totalEffectiveThrust < req.Length()) && wgv != 0;
                if (adjustmode) req = g[0].requiredVec;

                if (ShowMetrics)
                {
                    info.Append($"\n| {g[0].Role} |=>")
                    .Append($" |  {adjustmode} ");
                    vtprecision = 0;
                }

                for (int i = 0; i < g.Count; i++)
                {
                    VectorThrust vt = g[i];
                    IMyMotorStator rt = vt.rotor.TheBlock;

                    //0.067 - 126

                    if (CheckRotor(rt))
                    {
                        //0.074 - 136:132
                        float backup = vt.totalEffectiveThrust;
                        if ((thrustOn && !parked) || (!thrustOn && vt.activeThrusters.Count > 0)) vt.CalcTotalEffectiveThrust();

                        vt.requiredVec = adjustmode && req.Length() > vt.totalEffectiveThrust ? req.Normalized() * vt.totalEffectiveThrust : req;
                        if (adjustmode && req.Length() > vt.totalEffectiveThrust) req = req.Normalized() * (req.Length() - vt.totalEffectiveThrust);

                        //AVG R: 0.073 AVG I: 136

                        vt.Go();
                        //0.09 - 229 - 218

                        totaleffectivethrust += vt.totalEffectiveThrust;
                        totalVTThrprecision += vt.rotor.LastAngleCos;
                        total += req.Length();
                        vtprecision += vt.rotor.LastAngleCos;
                        j++;
                    }
                }
                if (ShowMetrics) info.Append($" |  {(vtprecision / g.Count).Round(1)}%  |\n");
            }

            // AVG RUNTIME: 0.078 - 0.095 AVG INSTRUCTIONS: 219 - min 215
            //WO : 0.071 - 121

            if (ShowMetrics) info.Append(edge);
            totalVTThrprecision /= j;

            totalaccel = totaleffectivethrust/myshipmass.PhysicalMass;

            
             tthrust += thrustbynthr;
             tthrust /= myshipmass.TotalMass;

            if (CanPrint())
            {
                echosb.AppendLine($"Total Force: {total.Round(0)}N\n");
                echosb = _RuntimeTracker.Append(echosb);

                if (ShowMetrics)
                {
                    echosb.AppendLine("--- Log ---");
                    echosb.Append(log);
                    screensb.Append(info);
                }

                log.Append(surfaceProviderErrorStr);
            }

            justCompiled = false;
            _RuntimeTracker.AddInstructions();
            // AVG RUNTIME: 0.09 - 0.10 AVG INSTRUCTIONS: 228
            // WO : 0.079 213

            // ========== END OF MAIN ==========
        }

        double tthrust = 0;
        Vector3D residuethrust = Vector3D.Zero;
        StringBuilder info;
        string edge;
        double vtprecision;
        double len = 0;
    }
}
