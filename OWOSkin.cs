using System;
using System.Collections.Generic;
using System.Threading;
using Bhaptics.SDK2;
using AfterTheFall_bhaptics;
using OWOGame;
using System.IO;
using System.Net;
using System.Threading.Tasks;


namespace MyBhapticsTactsuit
{

    public class OWOSkin
    {
        public bool suitDisabled = true;
        public bool systemInitialized = false;
        private static bool heartBeatIsActive = false;
        private static bool zombieGrabIsActive = false;
        private static bool zimplineIsActive = false;
        public static int heartBeatRate = 1000;
        public static string ziplineHand = "";
        public Dictionary<String, Sensation> FeedbackMap = new Dictionary<String, Sensation>();

        public OWOSkin()
        {
            RegisterAllSensationsFiles();
            InitializeOWO();
        }

        private void RegisterAllSensationsFiles()
        {
            string configPath = Directory.GetCurrentDirectory() + "\\Mods\\OWO";
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
                catch (Exception e) { Logger.LogError(e); }

            }

            systemInitialized = true;
        }

        private async void InitializeOWO()
        {
            Logger.LogInfo("Initializing OWO skin");

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
                Logger.LogInfo("OWO suit connected.");
            }
            if (suitDisabled) Logger.LogWarning("OWO is not enabled?!?!");
        }

        public BakedSensation[] AllBakedSensations()
        {
            var result = new List<BakedSensation>();

            foreach (var sensation in FeedbackMap.Values)
            {
                if (sensation is BakedSensation baked)
                {
                    Logger.LogInfo("Registered baked sensation: " + baked.name);
                    result.Add(baked);
                }
                else
                {
                    Logger.LogWarning("Sensation not baked? " + sensation);
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
                Logger.LogInfo("Manual IP file found: " + filePath);
                var lines = File.ReadLines(filePath);
                foreach (var line in lines)
                {
                    IPAddress address;
                    if (IPAddress.TryParse(line, out address)) ips.Add(line);
                    else Logger.LogWarning("IP not valid? ---" + line + "---");
                }
            }
            return ips.ToArray();
        }

        ~OWOSkin()
        {
            Logger.LogWarning("Destructor called");
            DisconnectOWO();
        }

        public void DisconnectOWO()
        {
            Logger.LogInfo("Disconnecting OWO skin.");
            OWO.Disconnect();
        }

        public void Feel(String key, int Priority, float intensity = 1.0f, float duration = 1.0f)
        {
            if (FeedbackMap.ContainsKey(key))
            {
                OWO.Send(FeedbackMap[key].WithPriority(Priority));
                Logger.LogInfo("SENSATION: " + key);
            }
            else Logger.LogWarning("Feedback not registered: " + key);
        }

        public async Task HeartBeatFuncAsync()
        {
            while (heartBeatIsActive)
            {
                Feel("HeartBeat", 0);
                await Task.Delay(heartBeatRate);
            }
        }
    }

    public class TactsuitVR
    {
        public bool suitDisabled = true;
        public bool systemInitialized = false;
        // Event to start and stop the heartbeat thread
        private static ManualResetEvent HeartBeat_mrse = new ManualResetEvent(false);
        private static ManualResetEvent ZombieGrab_mrse = new ManualResetEvent(false);
        private static ManualResetEvent ZipLine_mrse = new ManualResetEvent(false);
        public static int heartBeatRate = 1000;
        public static string ziplineHand = "";

        public void HeartBeatFunc()
        {
            while (true)
            {
                // Check if reset event is active
                HeartBeat_mrse.WaitOne();
                PlaybackHaptics("HeartBeat");
                Thread.Sleep(heartBeatRate);
            }
        }
        public void ZombieGrabFunc()
        {
            while (true)
            {
                // Check if reset event is active
                ZombieGrab_mrse.WaitOne();
                PlaybackHaptics("JuggernautGrab");
                Thread.Sleep(2000);
            }
        }
        public void ZipLineFunc()
        {
            while (true)
            {
                // Check if reset event is active
                ZipLine_mrse.WaitOne();
                PlaybackHaptics("RecoilHands_" + ziplineHand);
                PlaybackHaptics("ZiplineArms_" + ziplineHand); 
                PlaybackHaptics("ZiplineVest_" + ziplineHand);
                Thread.Sleep(500);
            }
        }

        public TactsuitVR()
        {

            LOG("Initializing suit");
            // Default configuration exported in the portal, in case the PC is not online
            var config = System.Text.Encoding.UTF8.GetString(AfterTheFall_bhaptics.Properties.Resource1.config);
            // Initialize with appID, apiKey, and default value in case it is unreachable
            var res = BhapticsSDK2.Initialize("VDgsXkzvLPIfwIBOTAX7", "uVIGumoIkQjWCnMxniVz", config);
            // if it worked, enable the suit
            suitDisabled = res != 0;

            LOG("Starting HeartBeat thread...");
            Thread HeartBeatThread = new Thread(HeartBeatFunc);
            HeartBeatThread.Start();
            Thread ZombieGrabThread = new Thread(ZombieGrabFunc);
            ZombieGrabThread.Start();
            Thread ZipLineThread = new Thread(ZipLineFunc);
            ZipLineThread.Start();
        }

        public void LOG(string logStr)
        {
            Plugin.Log.LogMessage(logStr);
        }

        public void PlaybackHaptics(String key, float intensity = 1.0f, float duration = 1.0f)
        {
            if (suitDisabled) return;
            BhapticsSDK2.Play(key.ToLower(), intensity, duration, 0f, 0f);
        }

        public void PlayBackHit(String key, float xzAngle, float yShift)
        {
            // two parameters can be given to the pattern to move it on the vest:
            // 1. An angle in degrees [0, 360] to turn the pattern to the left
            // 2. A shift [-0.5, 0.5] in y-direction (up and down) to move it up or down
            if (suitDisabled) { return; }
            if (BhapticsSDK2.IsDeviceConnected(PositionType.Head)) PlaybackHaptics("HeadShot");
            BhapticsSDK2.Play(key.ToLower(), 1f, 1f, xzAngle, yShift);
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
            PlaybackHaptics(keyHand, intensity);
            PlaybackHaptics(keyArm, intensity);
            PlaybackHaptics(keyVest, intensity);
            if (dualWield)
            {
                PlaybackHaptics(keyOtherHand, intensity);
                PlaybackHaptics(keyOtherArm, intensity);
            }
        }

        public void StartZipline()
        {
            ZipLine_mrse.Set();
        }

        public void StopZipline()
        {
            ZipLine_mrse.Reset();
        }

        public void StartZombieGrab()
        {
            ZombieGrab_mrse.Set();
        }

        public void StopZombieGrab()
        {
            ZombieGrab_mrse.Reset();
        }

        public void StartHeartBeat()
        {
            HeartBeat_mrse.Set();
        }

        public void StopHeartBeat()
        {
            HeartBeat_mrse.Reset();
        }

        public void StopHapticFeedback(String effect)
        {
            BhapticsSDK2.Stop(effect.ToLower());
        }

        public void StopAllHapticFeedback()
        {
            StopThreads();
            BhapticsSDK2.StopAll();
        }

        public void StopThreads()
        {
            StopHeartBeat();
            StopZombieGrab();
            StopZipline();
        }


    }
}
