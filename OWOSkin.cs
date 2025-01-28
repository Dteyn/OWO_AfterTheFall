using System;
using System.Collections.Generic;
using OWO_AfterTheFall;
using OWOGame;
using System.IO;
using System.Net;
using System.Threading.Tasks;


namespace OWOSKin
{

    public class OWOSkin
    {
        public bool suitDisabled = true;
        public bool systemInitialized = false;
        private static bool heartBeatIsActive = false;
        private static bool zombieGrabIsActive = false;
        private static bool ziplineIsActive = false;
        private static bool ziplineLIsActive = false;
        private static bool ziplineRIsActive = false;
        private int heartBeatRate = 1000;
        public Dictionary<String, Sensation> FeedbackMap = new Dictionary<String, Sensation>();

        public OWOSkin()
        {
            RegisterAllSensationsFiles();
            InitializeOWO();
        }

        public void LOG(string logStr, bool isWarning = false)
        {
            if (isWarning) Plugin.Log.LogWarning(logStr);
            else Plugin.Log.LogInfo(logStr);
        }

        public void UpdateHeartBeat(int newRate) => heartBeatRate = newRate; 

        private void RegisterAllSensationsFiles()
        {
            string configPath = Directory.GetCurrentDirectory() + "\\BepinEx\\Plugins\\OWO";
            DirectoryInfo d = new DirectoryInfo(configPath);
            FileInfo[] Files = d.GetFiles("*.owo", SearchOption.AllDirectories);
            for (int i = 0; i < Files.Length; i++)
            {
                string filename = Files[i].Name;
                string fullName = Files[i].FullName;
                string prefix = Path.GetFileNameWithoutExtension(filename);
                if (filename == "." || filename == "..")
                    continue;
                string tactFileStr = File.ReadAllText(fullName);
                try
                {
                    Sensation test = Sensation.Parse(tactFileStr);
                    FeedbackMap.Add(prefix, test);
                }
                catch (Exception e) { LOG(e.Message, true); }

            }

            systemInitialized = true;
        }

        private async void InitializeOWO()
        {
            LOG("Initializing OWO skin");

            var gameAuth = GameAuth.Create(AllBakedSensations()).WithId("0"); ;

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
            if (suitDisabled) LOG("OWO is not enabled?!?!", true);
        }

        public BakedSensation[] AllBakedSensations()
        {
            var result = new List<BakedSensation>();

            foreach (var sensation in FeedbackMap.Values)
            {
                if (sensation is BakedSensation baked)
                {
                    LOG("Registered baked sensation: " + baked.name);
                    result.Add(baked);
                }
                else
                {
                    LOG("Sensation not baked? " + sensation);
                    continue;
                }
            }
            return result.ToArray();
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
                    else LOG("IP not valid? ---" + line + "---", true);
                }
            }
            return ips.ToArray();
        }

        ~OWOSkin()
        {
            LOG("Destructor called");
            DisconnectOWO();
        }

        public void DisconnectOWO()
        {
            LOG("Disconnecting OWO skin.");
            OWO.Disconnect();
        }

        public void Feel(String key, int Priority, float intensity = 1.0f, float duration = 1.0f)
        {
            LOG("DEBUG-LOG-SENSATION: " + key);

            if (FeedbackMap.ContainsKey(key))
            {
                OWO.Send(FeedbackMap[key].WithPriority(Priority));
                LOG("SENSATION: " + key);
            }
            else LOG("Feedback not registered: " + key);
        }

        public async Task HeartBeatFuncAsync()
        {
            while (heartBeatIsActive)
            {
                Feel("HeartBeat", 0);
                await Task.Delay(heartBeatRate);
            }
        }

        public async Task ZombieGrabFuncAsync()
        {
            while (zombieGrabIsActive)
            {
                Feel("JuggernautGrab", 0);
                await Task.Delay(2000);                
            }
        }
        public async Task ZipLineFuncAsync()
        {
            LOG("ZIPLINE FUNC");
            //string toFeel = "";

            //while (ziplineRIsActive || ziplineLIsActive)
            //{
            //    if (ziplineRIsActive)
            //        toFeel = "Zipline_R";

            //    if (ziplineLIsActive)
            //        toFeel = "Zipline_L";

            //    if (ziplineRIsActive && ziplineLIsActive)
            //        toFeel = "Zipline_RL";

            //    Feel(toFeel, 2);
            //    await Task.Delay(500);
            //}

            ziplineIsActive = false;
        }

        public void StartHeartBeat()
        {
            if (heartBeatIsActive) return;

            heartBeatIsActive = true;
            HeartBeatFuncAsync();
        }

        public void StopHeartBeat()
        {
            heartBeatIsActive = false;
        }

        public void StartZipline(bool isRight)
        {
            if (isRight)
                ziplineRIsActive = true;

            if (!isRight)
                ziplineLIsActive = true;

            if (!ziplineIsActive)
            {
                ZipLineFuncAsync();
                ziplineIsActive = true;
            }

        }

        public void StopZipline()
        {
            ziplineRIsActive = false;
            ziplineLIsActive = false;
        }

        public void StartZombieGrab()
        {
            if (zombieGrabIsActive) return;

            zombieGrabIsActive = true;
            ZombieGrabFuncAsync();
        }

        public void StopZombieGrab()
        {
            zombieGrabIsActive = false;
        }

        public void StopAllHapticFeedback()
        {
            StopHeartBeat();
            StopZombieGrab();
            StopZipline();            

            OWO.Stop();
        }

        public void ShootRecoil(string gunType, bool isRightHand, bool dualWield = false, float intensity = 1.0f)
        {
            // Melee feedback pattern
            if (suitDisabled) { return; }
            if (gunType == "Pistol") intensity = 0.8f;
            string postfix = "_L";
            string otherPostfix = "_R";
            if (isRightHand) { postfix = "_R"; otherPostfix = "_L"; }
            string keyHand = "RecoilHands" + postfix;
            string keyOtherHand = "RecoilHands" + otherPostfix;
            string keyArm = "RecoilArms" + postfix;
            string keyOtherArm = "RecoilArms" + otherPostfix;
            string keyVest = "Recoil" + gunType + "Vest" + postfix;
            Feel(keyHand, 0);
            Feel(keyArm, 0);
            Feel(keyVest, 0);
            if (dualWield)
            {
                Feel(keyOtherHand, 0);
                Feel(keyOtherArm, 0);
            }
        }

        public void PlayBackHit(String key, float xzAngle, float yShift)
        {
            // two parameters can be given to the pattern to move it on the vest:
            // 1. An angle in degrees [0, 360] to turn the pattern to the left
            // 2. A shift [-0.5, 0.5] in y-direction (up and down) to move it up or down
            if (suitDisabled) { return; }
            OWO.Send(OWOGame.Sensation.Dart);
            //BhapticsSDK2.Play(key.ToLower(), 1f, 1f, xzAngle, yShift);
        }
    }
}
