using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SIGVerse.Common;
using SIGVerse.ToyotaHSR;
using System.Collections;

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
		public string environmentName;
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
		private const string TagDummyGraspingCandidates    = "DummyGraspingCandidates";
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

		private HandymanPlaybackRecorder playbackRecorder;



		public HandymanModeratorTool(List<GameObject> environments, HandymanScoreManager scoreManager, GameObject worldPlayback)
		{
			HandymanConfig.Instance.InclementNumberOfTrials();

			EnvironmentInfo environmentInfo = this.EnableEnvironment(environments);

			this.GetGameObjects(worldPlayback);

			this.Initialize(environmentInfo, scoreManager);
		}

		private EnvironmentInfo EnableEnvironment(List<GameObject> environments)
		{
			if(environments.Count != (from environment in environments select environment.name).Distinct().Count())
			{
				throw new Exception("There is the name conflict of environments.");
			}


			EnvironmentInfo environmentInfo = new EnvironmentInfo();

			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				GameObject activeEnvironment = (from environment in environments where environment.activeSelf==true select environment).FirstOrDefault();

				if(activeEnvironment!=null)
				{
					environmentInfo.environmentName = activeEnvironment.name;

					SIGVerseLogger.Warn("Selected an active environment. name=" + activeEnvironment.name);
				}
				else
				{
					environmentInfo.environmentName = environments[UnityEngine.Random.Range(0, environments.Count)].name;
				}
			}
			else
			{
				environmentInfo = this.GetEnvironmentInfo();
			}

			foreach (GameObject environment in environments)
			{
				if(environment.name==environmentInfo.environmentName)
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


		private void GetGameObjects(GameObject worldPlayback)
		{
			this.robot = GameObject.FindGameObjectWithTag(TagRobot);

			this.hsrBaseFootPrint = HSRCommon.FindGameObjectFromChild(this.robot.transform, HSRCommon.BaseFootPrintName);
			this.hsrGraspingDetector = this.robot.GetComponentInChildren<HSRGraspingDetector>();


			GameObject moderator = GameObject.FindGameObjectWithTag(TagModerator);


			// Get grasping candidates
			this.graspingCandidates = GameObject.FindGameObjectsWithTag(TagGraspingCandidates).ToList<GameObject>();


			if (this.graspingCandidates.Count == 0)
			{
				throw new Exception("Count of GraspingCandidates is zero.");
			}

			List<GameObject> dummyGraspingCandidates = GameObject.FindGameObjectsWithTag(TagDummyGraspingCandidates).ToList<GameObject>();

			this.graspables = new List<GameObject>();

			this.graspables.AddRange(this.graspingCandidates);
			this.graspables.AddRange(dummyGraspingCandidates);

			// Check the name conflict of graspables.
			if(this.graspables.Count != (from graspable in this.graspables select graspable.name).Distinct().Count())
			{
				throw new Exception("There is the name conflict of graspable objects.");
			}

			SIGVerseLogger.Info("Count of Graspables = " + this.graspables.Count);


			this.bedRoomArea = GameObject.Find(AreaNameBedRoom);
			this.kitchenArea = GameObject.Find(AreaNameKitchen);
			this.livingArea  = GameObject.Find(AreaNameLiving);
			this.lobbyArea   = GameObject.Find(AreaNameLobby);

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

			this.destinationCandidates.Add(moderator); // Treat moderator as a destination candidate

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


			this.playbackRecorder = worldPlayback.GetComponent<HandymanPlaybackRecorder>();
		}


		private void Initialize(EnvironmentInfo environmentInfo, HandymanScoreManager scoreManager)
		{
			List<GameObject> objectCollisionDestinations = new List<GameObject>();
			objectCollisionDestinations.Add(scoreManager.gameObject);

			foreach(GameObject graspable in this.graspables)
			{
				CollisionTransferer collisionTransferer = graspable.AddComponent<CollisionTransferer>();

				collisionTransferer.Initialize(objectCollisionDestinations, Score.GetObjectCollisionVeloticyThreshold());
			}


			Dictionary<RelocatableObjectInfo, GameObject> graspablesPositionMap = null; //key:GraspablePositionInfo, value:Graspables
			Dictionary<RelocatableObjectInfo, GameObject> destinationsPositionsMap = null; //key:DestinationPositionInfo, value:DestinationCandidate

			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				this.graspingTarget = this.DecideGraspingTarget();
				this.destination    = this.DecideDestination();

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
				this.SaveEnvironmentInfo(this.GetTaskMessage(), environmentInfo.environmentName, this.graspingTarget.name, this.destination.name, graspablesPositionMap, destinationsPositionsMap);
			}


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

		public string GetTaskMessage()
		{
			return "Go to the " + this.GetRoomName(this.targetRoom) + ", grasp the " + this.graspingTarget.name + " and send it to the " + this.destination.name + ".";
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
					+ this.graspingTarget.name + ", method name=(" + System.Reflection.MethodBase.GetCurrentMethod().Name + ")");
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
			Vector3 moderatorPos = FindGameObjectFromChild(moderatorRoot, DeliveryPositionName).transform.position;

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

		public static GameObject FindGameObjectFromChild(Transform root, string name)
		{
			Transform[] transforms = root.GetComponentsInChildren<Transform>();

			foreach (Transform transform in transforms)
			{
				if (transform.name == name)
				{
					return transform.gameObject;
				}
			}

			return null;
		}


		public void InitializePlayback()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				this.playbackRecorder.Initialize(HandymanConfig.Instance.numberOfTrials);
			}
		}


		public bool IsPlaybackInitialized()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				if(!this.playbackRecorder.IsInitialized()) { return false; }
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

		public void StopPlayback()
		{
			if (HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				bool isStopped = this.playbackRecorder.Stop();

				if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the world playback recording"); }
			}
		}

		public bool IsPlaybackFinished()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				if(!this.playbackRecorder.IsFinished()) { return false; }
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


		public void SaveEnvironmentInfo(string taskMessage, string environmentName, string graspingTargetName, string destinationName, Dictionary<RelocatableObjectInfo, GameObject> graspablesPositionMap, Dictionary<RelocatableObjectInfo, GameObject> destinationsPositionsMap)
		{
			EnvironmentInfo environmentInfo = new EnvironmentInfo();

			environmentInfo.taskMessage        = taskMessage;
			environmentInfo.environmentName    = environmentName;
			environmentInfo.graspingTargetName = graspingTargetName;
			environmentInfo.destinationName    = destinationName;

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

