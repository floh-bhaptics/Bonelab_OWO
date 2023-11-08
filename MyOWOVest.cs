using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using MelonLoader;
using OWOGame;
//using MyOWOSensations;

namespace MyOwoVest
{
    public class TactsuitVR
    {
        /* A class that contains the basic functions for the bhaptics Tactsuit, like:
         * - A Heartbeat function that can be turned on/off
         * - A function to read in and register all .tact patterns in the bHaptics subfolder
         * - A logging hook to output to the Melonloader log
         * - 
         * */
        public bool suitDisabled = true;
        public bool systemInitialized = false;
        // Event to start and stop the heartbeat thread
        public Dictionary<String, Sensation> FeedbackMap = new Dictionary<String, Sensation>();

        public TactsuitVR()
        {
            RegisterAllTactFiles();
            InitializeOWO();
        }

        private async void InitializeOWO()
        {
            LOG("Initializing suit");

            // New auth.
            var gameAuth = GameAuth.Create(AllBakedSensations()).WithId("40872061");

            OWO.Configure(gameAuth);
            string[] myIPs = getIPsFromFile("OWO_Manual_IP.txt");
            if (myIPs.Length == 0) await OWO.AutoConnect();
            else
            {
                await OWO.Connect(myIPs);
            }

            if (OWO.ConnectionState == ConnectionState.Connected)
            {
                suitDisabled = false;
                LOG("OWO suit connected.");
            }
            if (suitDisabled) LOG("Owo is not enabled?!?!");
        }

        public string[] getIPsFromFile(string filename)
        {
            List<string> ips = new List<string>();
            string filePath = Directory.GetCurrentDirectory() + "\\Mods\\" + filename;
            if (File.Exists(filePath))
            {
                LOG("Manual IP file found: " + filePath);
                var lines = File.ReadLines(filePath);
                foreach (var line in lines)
                {
                    IPAddress address;
                    if (IPAddress.TryParse(line, out address)) ips.Add(line);
                    else LOG("IP not valid? ---" + line + "---");
                }
            }
            return ips.ToArray();
        }

        ~TactsuitVR()
        {
            LOG("Destructor called");
            DisconnectOwo();
        }

        private BakedSensation[] AllBakedSensations()
        {
            var result = new List<BakedSensation>();

            foreach (var sensation in FeedbackMap.Values)
            {
                if (sensation is not BakedSensation baked)
                {
                    LOG("Sensation not baked? " + sensation);
                    continue;
                }
                LOG("Registered baked sensation: " + baked.name);
                result.Add(baked);
            }
            return result.ToArray();
        }

        public void DisconnectOwo()
        {
            LOG("Disconnecting Owo skin.");
            OWO.Disconnect();
        }

        public void LOG(string logStr)
        {
            MelonLogger.Msg(logStr);
        }

        void RegisterAllTactFiles()
        {

            string configPath = Directory.GetCurrentDirectory() + "\\Mods\\OWO";
            DirectoryInfo d = new DirectoryInfo(configPath);
            FileInfo[] Files = d.GetFiles("*.owo", SearchOption.AllDirectories);
            for (int i = 0; i < Files.Length; i++)
            {
                string filename = Files[i].Name;
                string fullName = Files[i].FullName;
                string prefix = Path.GetFileNameWithoutExtension(filename);
                // LOG("Trying to register: " + prefix + " " + fullName);
                if (filename == "." || filename == "..")
                    continue;
                string tactFileStr = File.ReadAllText(fullName);
                try
                {
                    Sensation test = Sensation.Parse(tactFileStr);
                    FeedbackMap.Add(prefix, test);
                }
                catch (Exception e) { LOG(e.ToString()); }

            }

            systemInitialized = true;
        }

        public string DetachFromMuscles(string pattern)
        {
            return System.Text.RegularExpressions.Regex.Replace(pattern, "\\|([0-9]%[0-9]+(,)*)+", "");
        }

        public void PlayBackHit(string pattern, float xzAngle, float yShift)
        {
            if (FeedbackMap.ContainsKey(pattern))
            {
                Sensation sensation = FeedbackMap[pattern];
                Muscle myMuscle = Muscle.Pectoral_R;
                // two parameters can be given to the pattern to move it on the vest:
                // 1. An angle in degrees [0, 360] to turn the pattern to the left
                // 2. A shift [-0.5, 0.5] in y-direction (up and down) to move it up or down
                if (sensation is SensationWithMuscles) { PlayBackFeedback(pattern); return; }
                if ((xzAngle < 90f))
                {
                    if (yShift >= 0f) myMuscle = Muscle.Pectoral_L;
                    else myMuscle = Muscle.Abdominal_L;
                }
                if ((xzAngle > 90f) && (xzAngle < 180f))
                {
                    if (yShift >= 0f) myMuscle = Muscle.Dorsal_L;
                    else myMuscle = Muscle.Lumbar_L;
                }
                if ((xzAngle > 180f) && (xzAngle < 270f))
                {
                    if (yShift >= 0f) myMuscle = Muscle.Dorsal_R;
                    else myMuscle = Muscle.Lumbar_R;
                }
                if ((xzAngle > 270f))
                {
                    if (yShift >= 0f) myMuscle = Muscle.Pectoral_R;
                    else myMuscle = Muscle.Abdominal_R;
                }
                PlayBackFeedback(pattern, myMuscle);
            }
            else
            {
                LOG("Feedback not registered: " + pattern);
                return;
            }

        }

        public void ArmHit(string pattern, bool isRightArm)
        {
            Sensation sensation = FeedbackMap[pattern];
            if (sensation is SensationWithMuscles) { PlayBackFeedback(pattern); return; }
            Muscle myMuscle = Muscle.Arm_L;
            if (isRightArm) myMuscle = Muscle.Arm_R;
            PlayBackFeedback(pattern, myMuscle);
        }

        public void GunRecoil(bool isRightHand, float intensity=1.0f, bool isTwoHanded=false, bool supportHand=true)
        {
            if (isTwoHanded)
            {
                PlayBackFeedback("Recoil_both");
                return;
            }
            if (isRightHand) PlayBackFeedback("Recoil_R");
            else PlayBackFeedback("Recoil_L");
        }

        public void PlayBackFeedback(string feedback)
        {
            if (FeedbackMap.ContainsKey(feedback))
            {
                OWO.Send(FeedbackMap[feedback]);
            }
            else LOG("Feedback not registered: " + feedback);
        }

        public void PlayBackFeedback(string feedback, Muscle onMuscle)
        {
            if (FeedbackMap.ContainsKey(feedback))
            {
                OWO.Send(FeedbackMap[feedback].WithMuscles(onMuscle));
            }
            else LOG("Feedback not registered: " + feedback);
        }

        public void PlayBackFeedback(string feedback, Muscle[] onMuscles)
        {
            if (FeedbackMap.ContainsKey(feedback))
            {
                OWO.Send(FeedbackMap[feedback].WithMuscles(onMuscles));
            }
            else LOG("Feedback not registered: " + feedback);
        }

    }
}
