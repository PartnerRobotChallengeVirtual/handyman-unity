using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SIGVerse.Common;
using SIGVerse.ToyotaHSR;
using System.Collections;
using SIGVerse.RosBridge;
using SIGVerse.SIGVerseRosBridge;

namespace SIGVerse.Competition.Handyman
{
	[Serializable]
	public class RelocatableObjectInfo
	{
		public string name;
		public Vector3 position;
		public Vector3 eulerAngles;
	}

	[Serializable]
	public class EnvironmentInfo
	{
		public string taskMessage;
		public string correctedTaskMessage;
		public string environmentName;
		public bool   isEnvironmentNameSent;
		public string graspingTargetName;
		public string destinationName;
		public List<RelocatableObjectInfo> graspablesPositions;
		public List<RelocatableObjectInfo> destinationsPositions; 
	}

	public class HandymanModeratorTool
	{
		private const string EnvironmentInfoFileNameFormat = "/../SIGVerseConfig/Handyman/EnvironmentInfo{0:D2}.json";

		private const string TagRobot                      = "Robot";
		private const string TagModerator                  = "Moderator";
		private const string TagGraspingCandidates         = "GraspingCandidates";
//		private const string TagDummyGraspingCandidates    = "DummyGraspingCandidates";
		private const string TagGraspingCandidatesPosition = "GraspingCandidatesPosition";
		private const string TagDestinationCandidates      = "DestinationCandidates";

		private const string JudgeTriggersName    = "JudgeTriggers";
		private const string DeliveryPositionName = "DeliveryPosition";

		private const float  DeliveryThreshold = 0.3f;

		private const string AreaNameBedRoom = "BedRoomArea";
		private const string AreaNameKitchen = "KitchenArea";
		private const string AreaNameLiving  = "LivingArea";
		private const string AreaNameLobby   = "LobbyArea";

		private const string RoomNameForTaskMessageKitchen = "kitchen";
		private const string RoomNameForTaskMessageLobby   = "lobby";
		private const string RoomNameForTaskMessageBedRoom = "bed room";
		private const string RoomNameForTaskMessageLiving  = "living room";

		public const string SpeechExePath  = "../TTS/ConsoleSimpleTTS.exe";
		public const string SpeechLanguage = "409";
		public const string SpeechGender   = "Male";


		private IRosConnection[] rosConnections;

		private string environmentName;
		private string taskMessage;
		private string correctedTaskMessage;
		private bool   isEnvironmentNameSent;

		private GameObject graspingTarget;
		private List<GameObject> graspables;
		private List<GameObject> graspingCandidates;

		private List<GameObject> graspingCandidatesPositions;

		private GameObject destination;
		private List<GameObject> destinationCandidates;

		private GameObject robot;
		private Transform hsrBaseFootPrint;
		private HSRGraspingDetector hsrGraspingDetector;


		private GameObject targetRoom;

		private GameObject bedRoomArea;
		private GameObject kitchenArea;
		private GameObject livingArea;
		private GameObject lobbyArea;

		private bool? isPlacementSucceeded;

		private HandymanAvatarMotionPlayer   avatarMotionPlayer;
		private HandymanAvatarMotionRecorder avatarMotionRecorder;

		private HandymanPlaybackRecorder playbackRecorder;

		private System.Diagnostics.Process speechProcess;


		public HandymanModeratorTool(HandymanModerator moderator)
		{
			HandymanConfig.Instance.InclementNumberOfTrials();

			EnvironmentInfo environmentInfo = this.EnableEnvironment(moderator.environments);

			this.GetGameObjects(moderator.avatarMotionPlayback, moderator.playbackManager);

			this.Initialize(environmentInfo, moderator.scoreManager, moderator.objectCollisionAudioSource);
		}

		private EnvironmentInfo EnableEnvironment(List<GameObject> environments)
		{
			if(environments.Count != (from environment in environments select environment.name).Distinct().Count())
			{
				throw new Exception("There is the name conflict of environments.");
			}


			EnvironmentInfo environmentInfo = null;

			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				GameObject activeEnvironment = (from environment in environments where environment.activeSelf==true select environment).FirstOrDefault();

				if(activeEnvironment!=null)
				{
					this.environmentName = activeEnvironment.name;

					SIGVerseLogger.Warn("Selected an active environment. name=" + activeEnvironment.name);
				}
				else
				{
					this.environmentName = environments[UnityEngine.Random.Range(0, environments.Count)].name;
				}
			}
			else
			{
				environmentInfo = this.GetEnvironmentInfo();

				this.environmentName = environmentInfo.environmentName;
			}

			foreach (GameObject environment in environments)
			{
				if(environment.name==this.environmentName)
				{
					environment.SetActive(true);
				}
				else
				{
					environment.SetActive(false);
				}
			}

			return environmentInfo;
		}


		private void GetGameObjects(GameObject avatarMotionPlayback, GameObject worldPlayback)
		{
			this.robot = GameObject.FindGameObjectWithTag(TagRobot);

			this.hsrBaseFootPrint = SIGVerseUtils.FindTransformFromChild(this.robot.transform, HSRCommon.BaseFootPrintName);
			this.hsrGraspingDetector = this.robot.GetComponentInChildren<HSRGraspingDetector>();


			GameObject moderatorObj = GameObject.FindGameObjectWithTag(TagModerator);


			// Get grasping candidates
			this.graspingCandidates = GameObject.FindGameObjectsWithTag(TagGraspingCandidates).ToList<GameObject>();


			if (this.graspingCandidates.Count == 0)
			{
				throw new Exception("Count of GraspingCandidates is zero.");
			}

//			List<GameObject> dummyGraspingCandidates = GameObject.FindGameObjectsWithTag(TagDummyGraspingCandidates).ToList<GameObject>();

			this.graspables = new List<GameObject>();

			this.graspables.AddRange(this.graspingCandidates);
//			this.graspables.AddRange(dummyGraspingCandidates);

			// Check the name conflict of graspables.
			if(this.graspables.Count != (from graspable in this.graspables select graspable.name).Distinct().Count())
			{
				throw new Exception("There is the name conflict of graspable objects.");
			}

			SIGVerseLogger.Info("Count of Graspables = " + this.graspables.Count);


			this.bedRoomArea = GameObject.Find(this.environmentName+"/RoomArea/"+AreaNameBedRoom);
			this.kitchenArea = GameObject.Find(this.environmentName+"/RoomArea/"+AreaNameKitchen);
			this.livingArea  = GameObject.Find(this.environmentName+"/RoomArea/"+AreaNameLiving);
			this.lobbyArea   = GameObject.Find(this.environmentName+"/RoomArea/"+AreaNameLobby);

			// Get grasping candidates positions
			this.graspingCandidatesPositions = GameObject.FindGameObjectsWithTag(TagGraspingCandidatesPosition).ToList<GameObject>();

			if (this.graspables.Count > this.graspingCandidatesPositions.Count)
			{
				throw new Exception("graspables.Count > graspingCandidatesPositions.Count.");
			}
			else
			{
				SIGVerseLogger.Info("Count of GraspingCandidatesPosition = " + this.graspingCandidatesPositions.Count);
			}


			this.destinationCandidates = GameObject.FindGameObjectsWithTag(TagDestinationCandidates).ToList<GameObject>();

			this.destinationCandidates.Add(moderatorObj); // Treat moderator as a destination candidate

			if(this.destinationCandidates.Count == 0)
			{
				throw new Exception("Count of DestinationCandidates is zero.");
			}

			// Check the name conflict of destination candidates.
			if(this.destinationCandidates.Count != (from destinations in this.destinationCandidates select destinations.name).Distinct().Count())
			{
				throw new Exception("There is the name conflict of destination candidates objects.");
			}

			SIGVerseLogger.Info("Count of Destinations = " + this.destinationCandidates.Count);

			this.avatarMotionPlayer   = avatarMotionPlayback.GetComponent<HandymanAvatarMotionPlayer>();
			this.avatarMotionRecorder = avatarMotionPlayback.GetComponent<HandymanAvatarMotionRecorder>();

			this.playbackRecorder = worldPlayback.GetComponent<HandymanPlaybackRecorder>();
		}


		private void Initialize(EnvironmentInfo environmentInfo, HandymanScoreManager scoreManager, AudioSource objectCollisionAudioSource)
		{
			List<GameObject> objectCollisionDestinations = new List<GameObject>();
			objectCollisionDestinations.Add(scoreManager.gameObject);
			objectCollisionDestinations.Add(this.playbackRecorder.gameObject);

			foreach(GameObject graspable in this.graspables)
			{
				CollisionTransferer collisionTransferer = graspable.AddComponent<CollisionTransferer>();

				collisionTransferer.Initialize(objectCollisionDestinations, Score.GetObjectCollisionVeloticyThreshold(), 0.1f, objectCollisionAudioSource);
			}


			Dictionary<RelocatableObjectInfo, GameObject> graspablesPositionMap = null; //key:GraspablePositionInfo, value:Graspables
			Dictionary<RelocatableObjectInfo, GameObject> destinationsPositionsMap = null; //key:DestinationPositionInfo, value:DestinationCandidate

			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				this.graspingTarget        = this.DecideGraspingTarget();
				this.destination           = this.DecideDestination();

				graspablesPositionMap    = this.CreateGraspablesPositionMap();
				destinationsPositionsMap = this.CreateDestinationsPositionsMap();
			}
			else
			{
				this.DeactivateGraspingCandidatesPositions();

				this.graspingTarget = (from graspable in this.graspables where graspable.name == environmentInfo.graspingTargetName select graspable).First();

				if(this.graspingTarget==null) { throw new Exception("Grasping target not found. name=" + environmentInfo.graspingTargetName); }

				graspablesPositionMap = new Dictionary<RelocatableObjectInfo, GameObject>();

				foreach(RelocatableObjectInfo graspablePositionInfo in environmentInfo.graspablesPositions)
				{
					GameObject graspableObj = (from graspable in this.graspables where graspable.name == graspablePositionInfo.name select graspable).First();

					if (graspableObj==null) { throw new Exception("Graspable object not found. name=" + graspablePositionInfo.name); }

					graspablesPositionMap.Add(graspablePositionInfo, graspableObj);
				}


				// Destination object
				this.destination = (from destinationCandidate in this.destinationCandidates where destinationCandidate.name == environmentInfo.destinationName select destinationCandidate).First();

				if (this.destination == null) { throw new Exception("Destination not found. name=" + environmentInfo.destinationName); }

				// Destination candidates position map
				destinationsPositionsMap = new Dictionary<RelocatableObjectInfo, GameObject>();

				foreach (RelocatableObjectInfo destinationPositionInfo in environmentInfo.destinationsPositions)
				{
					GameObject destinationObj = (from destinationCandidate in this.destinationCandidates where destinationCandidate.name == destinationPositionInfo.name select destinationCandidate).First();

					if (destinationObj == null) { throw new Exception("Destination candidate not found. name=" + destinationPositionInfo.name); }

					destinationsPositionsMap.Add(destinationPositionInfo, destinationObj);
				}
			}


			if(this.destination.tag!=TagModerator)
			{ 
				// Add Placement checker to triggers
				Transform judgeTriggersTransform = this.destination.transform.Find(JudgeTriggersName);

				if (judgeTriggersTransform==null) { throw new Exception("No Judge Triggers object"); }

				judgeTriggersTransform.gameObject.AddComponent<PlacementChecker>();
			}

			
			foreach (KeyValuePair<RelocatableObjectInfo, GameObject> pair in graspablesPositionMap)
			{
				pair.Value.transform.position    = pair.Key.position;
				pair.Value.transform.eulerAngles = pair.Key.eulerAngles;

//				Debug.Log(pair.Key.name + " : " + pair.Value.name);
			}

			foreach (KeyValuePair<RelocatableObjectInfo, GameObject> pair in destinationsPositionsMap)
			{
				pair.Value.transform.position    = pair.Key.position;
				pair.Value.transform.eulerAngles = pair.Key.eulerAngles;

//				Debug.Log(pair.Key.name + " : " + pair.Value.name);
			}

			this.targetRoom = this.GetTargetRoom();

			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				this.taskMessage           = this.CreateTaskMessage();
				this.correctedTaskMessage  = string.Empty;
				this.isEnvironmentNameSent = true;
			}
			else
			{
				this.taskMessage          = environmentInfo.taskMessage;
				this.correctedTaskMessage = environmentInfo.correctedTaskMessage;

				this.isEnvironmentNameSent = environmentInfo.isEnvironmentNameSent;
			}


			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				SaveEnvironmentInfo(this.taskMessage, this.correctedTaskMessage, this.environmentName, this.isEnvironmentNameSent, this.graspingTarget.name, this.destination.name, graspablesPositionMap, destinationsPositionsMap);
			}

			this.rosConnections = SIGVerseUtils.FindObjectsOfInterface<IRosConnection>();

			SIGVerseLogger.Info("ROS connection : count=" + this.rosConnections.Length);


			// Set up the voice (Using External executable file)
			this.speechProcess = new System.Diagnostics.Process();

			this.speechProcess.StartInfo.FileName = Application.dataPath + "/" + SpeechExePath;

			this.speechProcess.StartInfo.CreateNoWindow = true;
			this.speechProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;

			SIGVerseLogger.Info("Text-To-Speech: " + this.speechProcess.StartInfo.FileName);


			this.isPlacementSucceeded   = null;
		}


		public List<GameObject> GetGraspables()
		{
			return this.graspables;
		}


		public IEnumerator LoosenRigidbodyConstraints(Rigidbody rigidbody)
		{
			while(!rigidbody.IsSleeping())
			{
				yield return null;
			}

			rigidbody.constraints = RigidbodyConstraints.None;
		}


		public GameObject DecideGraspingTarget()
		{
			// Decide the grasping target
			GameObject graspingTarget = this.graspingCandidates[UnityEngine.Random.Range(0, this.graspingCandidates.Count)];

			SIGVerseLogger.Info("Grasping target is " + graspingTarget.name);

			return graspingTarget;
		}


		public GameObject DecideDestination()
		{
			// Decide the destination
			GameObject destination = this.destinationCandidates[UnityEngine.Random.Range(0, this.destinationCandidates.Count)];

			SIGVerseLogger.Info("Destination is " + destination.name);

			return destination;
		}


		public void DeactivateGraspingCandidatesPositions()
		{
			foreach (GameObject graspingCandidatesPosition in this.graspingCandidatesPositions)
			{
				graspingCandidatesPosition.SetActive(false);
			}
		}

		public Dictionary<RelocatableObjectInfo, GameObject> CreateGraspablesPositionMap()
		{
			List<GameObject> graspingCandidatesPositionsInBedRoom = new List<GameObject>();
			List<GameObject> graspingCandidatesPositionsInKitchen = new List<GameObject>();
			List<GameObject> graspingCandidatesPositionsInLiving  = new List<GameObject>();
			List<GameObject> graspingCandidatesPositionsInLobby   = new List<GameObject>();

			this.DeactivateGraspingCandidatesPositions();

			foreach (GameObject graspingCandidatesPosition in this.graspingCandidatesPositions)
			{
				Vector3 position = graspingCandidatesPosition.transform.position;

				if (this.IsTargetInArea(position, this.bedRoomArea)) { graspingCandidatesPositionsInBedRoom.Add(graspingCandidatesPosition); }
				if (this.IsTargetInArea(position, this.kitchenArea)) { graspingCandidatesPositionsInKitchen.Add(graspingCandidatesPosition); }
				if (this.IsTargetInArea(position, this.livingArea))  { graspingCandidatesPositionsInLiving .Add(graspingCandidatesPosition); }
				if (this.IsTargetInArea(position, this.lobbyArea))   { graspingCandidatesPositionsInLobby  .Add(graspingCandidatesPosition); }
			}

			// Shuffle the grasping candidates list
			this.graspables = this.graspables.OrderBy(i => Guid.NewGuid()).ToList();

			// Shuffle the grasping candidates position list
			graspingCandidatesPositionsInBedRoom = graspingCandidatesPositionsInBedRoom.OrderBy(i => Guid.NewGuid()).ToList();
			graspingCandidatesPositionsInKitchen = graspingCandidatesPositionsInKitchen.OrderBy(i => Guid.NewGuid()).ToList();
			graspingCandidatesPositionsInLiving  = graspingCandidatesPositionsInLiving .OrderBy(i => Guid.NewGuid()).ToList();
			graspingCandidatesPositionsInLobby   = graspingCandidatesPositionsInLobby  .OrderBy(i => Guid.NewGuid()).ToList();


			List<GameObject> graspingCandidatesPositionsTmp = new List<GameObject>();

			for (int i=0; graspingCandidatesPositionsTmp.Count < this.graspables.Count; i++)
			{
				if (graspingCandidatesPositionsInBedRoom.Count > i){ graspingCandidatesPositionsTmp.Add(graspingCandidatesPositionsInBedRoom[i]); }
				if (graspingCandidatesPositionsInKitchen.Count > i){ graspingCandidatesPositionsTmp.Add(graspingCandidatesPositionsInKitchen[i]); }
				if (graspingCandidatesPositionsInLiving.Count  > i){ graspingCandidatesPositionsTmp.Add(graspingCandidatesPositionsInLiving[i]); }
				if (graspingCandidatesPositionsInLobby.Count   > i){ graspingCandidatesPositionsTmp.Add(graspingCandidatesPositionsInLobby[i]); }
			}

			Dictionary<RelocatableObjectInfo, GameObject> graspingCandidatesMap = new Dictionary<RelocatableObjectInfo, GameObject>();

			for (int i=0; i<this.graspables.Count; i++)
			{
				RelocatableObjectInfo graspablePositionInfo = new RelocatableObjectInfo();

				graspablePositionInfo.name        = this.graspables[i].name;
				graspablePositionInfo.position    = graspingCandidatesPositionsTmp[i].transform.position - new Vector3(0, graspingCandidatesPositionsTmp[i].transform.localScale.y * 0.49f, 0);
				graspablePositionInfo.eulerAngles = graspingCandidatesPositionsTmp[i].transform.eulerAngles;

				graspingCandidatesMap.Add(graspablePositionInfo, this.graspables[i]);
			}

			return graspingCandidatesMap;
		}


		public Dictionary<RelocatableObjectInfo, GameObject> CreateDestinationsPositionsMap()
		{
			Dictionary<RelocatableObjectInfo, GameObject> destinationsPositionsMap = new Dictionary<RelocatableObjectInfo, GameObject>();

			for (int i=0; i<this.destinationCandidates.Count; i++)
			{
				RelocatableObjectInfo destinationPositionInfo = new RelocatableObjectInfo();

				destinationPositionInfo.name        = this.destinationCandidates[i].name;
				destinationPositionInfo.position    = this.destinationCandidates[i].transform.position;
				destinationPositionInfo.eulerAngles = this.destinationCandidates[i].transform.eulerAngles;

				destinationsPositionsMap.Add(destinationPositionInfo, this.destinationCandidates[i]);
			}

			return destinationsPositionsMap;
		}

		public string GetRoomName(GameObject roomObj)
		{
			if (roomObj == this.bedRoomArea) { return RoomNameForTaskMessageBedRoom; }
			if (roomObj == this.kitchenArea) { return RoomNameForTaskMessageKitchen; }
			if (roomObj == this.livingArea)  { return RoomNameForTaskMessageLiving; }
			if (roomObj == this.lobbyArea)   { return RoomNameForTaskMessageLobby; }

			throw new Exception("There is no grasping target in the 4 rooms. Grasping target =");
		}

		public string GetEnvironmentName()
		{
			if(this.isEnvironmentNameSent)
			{
				return this.environmentName;
			}
			else
			{
				return string.Empty;
			}
		}

		private string CreateTaskMessage()
		{
			return "Go to the " + this.GetRoomName(this.targetRoom) + ", grasp the " + this.graspingTarget.name.Split('#')[0] + " and send it to the " + this.destination.name.Split('#')[0] + ".";
		}

		public string GetTaskMessage()
		{
			return this.taskMessage;
		}

		public string GetCorrectedTaskMessage()
		{
			return this.correctedTaskMessage;
		}


		public GameObject GetTargetRoom()
		{
			if      (this.IsTargetInArea(this.graspingTarget.transform.position, this.bedRoomArea)){ return this.bedRoomArea; }
			else if (this.IsTargetInArea(this.graspingTarget.transform.position, this.kitchenArea)){ return this.kitchenArea; }
			else if (this.IsTargetInArea(this.graspingTarget.transform.position, this.livingArea)) { return this.livingArea; }
			else if (this.IsTargetInArea(this.graspingTarget.transform.position, this.lobbyArea))  { return this.lobbyArea; }
			else
			{
				throw new Exception("There is no grasping target in the 4 rooms. Grasping target = " 
					+ this.graspingTarget.name + ", position="+this.graspingTarget.transform.position+", method name=(" + System.Reflection.MethodBase.GetCurrentMethod().Name + ")");
			}
		}


		public bool IsRoomReachedSucceeded()
		{
			return this.IsTargetInArea(this.hsrBaseFootPrint.position, this.targetRoom, 0.215f);
		}


		public bool IsTargetInArea(Vector3 targetPosition, GameObject area, float margin)
		{
			Vector3 roomPosition = area.transform.position;

			BoxCollider boxCollider = area.GetComponent<BoxCollider>();

			float xMin = roomPosition.x + boxCollider.center.x - boxCollider.size.x / 2.0f + margin;
			float xMax = roomPosition.x + boxCollider.center.x + boxCollider.size.x / 2.0f - margin;
			float zMin = roomPosition.z + boxCollider.center.z - boxCollider.size.z / 2.0f + margin;
			float zMax = roomPosition.z + boxCollider.center.z + boxCollider.size.z / 2.0f - margin;

			if (xMin < targetPosition.x & targetPosition.x < xMax && zMin < targetPosition.z & targetPosition.z < zMax)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		public bool IsTargetInArea(Vector3 targetPosition, GameObject area)
		{
			return IsTargetInArea(targetPosition, area, 0.0f);
		}


		public IEnumerator Speak(string message, float delay = 0.0f, bool needsCancelLatestSpeech = false)
		{
			yield return new WaitForSeconds(delay);

			try
			{
				if (needsCancelLatestSpeech && !this.speechProcess.HasExited)
				{
					this.speechProcess.Kill();
				}
			}
			catch (Exception)
			{
				// Do nothing even if an error occurs
			}

			yield return null;


			message = message.Replace("_", " "); // Remove "_"

			this.speechProcess.StartInfo.Arguments = "\"" + message + "\" \"Language=" + SpeechLanguage + "; Gender=" + SpeechGender + "\"";

			try
			{
				this.speechProcess.Start();

				SIGVerseLogger.Info("Moderator spoke :" + message);
			}
			catch (Exception)
			{
				SIGVerseLogger.Warn("Moderator could not speak :" + message);
			}
		}

		public IEnumerator SpeakGood(float delay = 0.0f)
		{
			yield return Speak("Good!", delay, false);
		}

		public IEnumerator SpeakFailed(float delay = 0.0f)
		{
			yield return Speak("Failed", delay, true);
		}


		public bool IsObjectGraspedSucceeded()
		{
			if (this.hsrGraspingDetector.GetGraspedObject() != null)
			{
				return this.graspingTarget == this.hsrGraspingDetector.GetGraspedObject();
			}

			return false;
		}

		public bool IsPlacementCheckFinished()
		{
			return isPlacementSucceeded != null;
		}

		public bool IsPlacementSucceeded()
		{
			return (bool)isPlacementSucceeded;
		}

		private bool IsDeliverySucceeded(Transform moderatorRoot)
		{
			Rigidbody targetRigidbody = this.graspingTarget.GetComponent<Rigidbody>();

			Vector3 targetPos    = targetRigidbody.transform.TransformPoint(targetRigidbody.centerOfMass);
			Vector3 moderatorPos = SIGVerseUtils.FindTransformFromChild(moderatorRoot, DeliveryPositionName).position;

			return Vector3.Distance(targetPos, moderatorPos) <= DeliveryThreshold && this.IsObjectGraspedSucceeded();
		}


		public IEnumerator UpdatePlacementStatus(MonoBehaviour moderator)
		{
			if(this.destination.tag == TagModerator)
			{
				this.isPlacementSucceeded = this.IsDeliverySucceeded(moderator.transform.root);
			}
			else
			{
				if(this.graspingTarget.transform.root == this.robot.transform.root)
				{
					this.isPlacementSucceeded = false;

					SIGVerseLogger.Info("Target placement failed: HSR has the grasping target.");
				}
				else
				{
					PlacementChecker placementChecker = this.destination.GetComponentInChildren<PlacementChecker>();

					IEnumerator<bool?> isPlaced = placementChecker.IsPlaced(this.graspingTarget);

					yield return moderator.StartCoroutine(isPlaced);

					this.isPlacementSucceeded = (bool)isPlaced.Current;
				}
			}
		}


		public void InitializePlayback()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				this.playbackRecorder.Initialize(HandymanConfig.Instance.numberOfTrials);
			}

			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				this.avatarMotionRecorder.Initialize(HandymanConfig.Instance.numberOfTrials);
			}
			else
			{
				this.avatarMotionPlayer.Initialize(HandymanConfig.Instance.numberOfTrials);
			}
		}


		public bool IsConnectedToRos()
		{
			foreach(IRosConnection rosConnection in this.rosConnections)
			{
				if(!rosConnection.IsConnected())
				{
					return false;
				}
			}
			return true;
		}

		public IEnumerator ClearRosConnections()
		{
			yield return new WaitForSecondsRealtime (1.5f);

			foreach(IRosConnection rosConnection in this.rosConnections)
			{
				rosConnection.Clear();
			}

			SIGVerseLogger.Info("Clear ROS connections");
		}

		public IEnumerator CloseRosConnections()
		{
			yield return new WaitForSecondsRealtime (1.5f);

			foreach(IRosConnection rosConnection in this.rosConnections)
			{
				rosConnection.Close();
			}

			SIGVerseLogger.Info("Close ROS connections");
		}

		public bool IsPlaybackInitialized()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				if(!this.playbackRecorder.IsInitialized()) { return false; }
			}

			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				if(!this.avatarMotionRecorder.IsInitialized()) { return false; }
			}
			else
			{
				if (!this.avatarMotionPlayer.IsInitialized()) { return false; }
			}

			return true;
		}


		public void StartPlayback()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				bool isStarted = this.playbackRecorder.Record();

				if(!isStarted) { SIGVerseLogger.Warn("Cannot start the world playback recording"); }
			}
		}

		public void StartAvatarMotionPlayback()
		{
			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				bool isStarted = this.avatarMotionRecorder.Record();

				if(!isStarted) { SIGVerseLogger.Warn("Cannot start the avatar motion recording"); }
			}
			else
			{
				bool isStarted = this.avatarMotionPlayer.Play();

				if(!isStarted) { SIGVerseLogger.Warn("Cannot start the avatar motion playing"); }
			}
		}

		public void StopPlayback()
		{
			if (HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				bool isStopped = this.playbackRecorder.Stop();

				if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the world playback recording"); }
			}

			this.StopAvatarMotionPlayback();
		}

		private void StopAvatarMotionPlayback()
		{
			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				bool isStopped = this.avatarMotionRecorder.Stop();

				if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the avatar motion recording"); }
			}
			else
			{
				bool isStopped = this.avatarMotionPlayer.Stop();

				if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the avatar motion playing"); }
			}
		}

		public bool IsPlaybackFinished()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				if(!this.playbackRecorder.IsFinished()) { return false; }
			}

			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				if(!this.avatarMotionRecorder.IsFinished()) { return false; }
			}
			else
			{
				if(!this.avatarMotionPlayer.IsFinished()) { return false; }
			}

			return true;
		}


		public EnvironmentInfo GetEnvironmentInfo()
		{
			string filePath = String.Format(Application.dataPath + EnvironmentInfoFileNameFormat, HandymanConfig.Instance.numberOfTrials);

			EnvironmentInfo environmentInfo = new EnvironmentInfo();

			if (File.Exists(filePath))
			{
				// File open
				StreamReader streamReader = new StreamReader(filePath, Encoding.UTF8);

				environmentInfo = JsonUtility.FromJson<EnvironmentInfo>(streamReader.ReadToEnd());

				streamReader.Close();
			}
			else
			{
				throw new Exception("Environment info file does not exist. filePath=" + filePath);
			}

			return environmentInfo;
		}


		private static void SaveEnvironmentInfo(string taskMessage, string correctedTaskMessage, string environmentName, bool isEnvironmentNameSent, string graspingTargetName, string destinationName, Dictionary<RelocatableObjectInfo, GameObject> graspablesPositionMap, Dictionary<RelocatableObjectInfo, GameObject> destinationsPositionsMap)
		{
			EnvironmentInfo environmentInfo = new EnvironmentInfo();

			environmentInfo.taskMessage           = taskMessage;
			environmentInfo.correctedTaskMessage  = correctedTaskMessage;
			environmentInfo.environmentName       = environmentName;
			environmentInfo.isEnvironmentNameSent = isEnvironmentNameSent;
			environmentInfo.graspingTargetName    = graspingTargetName;
			environmentInfo.destinationName       = destinationName;

			List<RelocatableObjectInfo> graspablesPositions = new List<RelocatableObjectInfo>();

			foreach(KeyValuePair<RelocatableObjectInfo, GameObject> graspablePositionPair in graspablesPositionMap)
			{
				RelocatableObjectInfo graspableInfo = new RelocatableObjectInfo();
				graspableInfo.name        = graspablePositionPair.Value.name;
				graspableInfo.position    = graspablePositionPair.Key.position;
				graspableInfo.eulerAngles = graspablePositionPair.Key.eulerAngles;

				graspablesPositions.Add(graspableInfo);
			}

			environmentInfo.graspablesPositions = graspablesPositions;


			List<RelocatableObjectInfo> destinationsPositions = new List<RelocatableObjectInfo>();

			foreach(KeyValuePair<RelocatableObjectInfo, GameObject> destinationPositionPair in destinationsPositionsMap)
			{
				RelocatableObjectInfo destinationInfo = new RelocatableObjectInfo();
				destinationInfo.name        = destinationPositionPair.Value.name;
				destinationInfo.position    = destinationPositionPair.Key.position;
				destinationInfo.eulerAngles = destinationPositionPair.Key.eulerAngles;

				destinationsPositions.Add(destinationInfo);
			}

			environmentInfo.destinationsPositions = destinationsPositions;


			string filePath = String.Format(Application.dataPath + EnvironmentInfoFileNameFormat, HandymanConfig.Instance.numberOfTrials);

			StreamWriter streamWriter = new StreamWriter(filePath, false, Encoding.UTF8);

			SIGVerseLogger.Info("Save Environment info. path=" + filePath);

			streamWriter.WriteLine(JsonUtility.ToJson(environmentInfo, true));

			streamWriter.Flush();
			streamWriter.Close();
		}
	}
}

