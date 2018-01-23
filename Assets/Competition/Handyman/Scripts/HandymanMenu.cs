using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using SIGVerse.Common;

namespace SIGVerse.Competition.Handyman
{
	public class HandymanMenu : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		[HeaderAttribute("Panels")]
		public GameObject mainPanel;
		public GameObject giveUpPanel;
		public GameObject scorePanel;
		public GameObject notice;

		[HeaderAttribute("HandymanModerator")]
		public HandymanModerator moderator;

		[HeaderAttribute("Audience camera")]
		public List<Camera> audienceCameras;


		//---------------------------------------------------
		private GameObject draggingPanel;

		private Image mainPanelImage;
		private GameObject targetsOfHiding;

		private bool isMainPanelVisible;
		private bool isGiveUpPanelVisible;
		private bool isScorePanelVisible;

		private int mainAudienceCameraNo;

		// Use this for initialization
		void Start()
		{
			this.mainPanelImage = this.mainPanel.GetComponent<Image>();

			this.targetsOfHiding = this.mainPanel.transform.Find("TargetsOfHiding").gameObject;

			this.mainPanel.SetActive(true);
			this.giveUpPanel.SetActive(false);
			this.scorePanel.SetActive(true);
			this.notice.SetActive(false);

			this.mainAudienceCameraNo = 0;

			this.UpdateAudienceCameraDepth();
		}

		// Update is called once per frame
		void Update()
		{
		}

		public void ClickHiddingButton()
		{
			if (this.mainPanelImage.enabled)
			{
				this.isMainPanelVisible   = this.mainPanelImage.enabled;
				this.isGiveUpPanelVisible = this.giveUpPanel.activeSelf;
				this.isScorePanelVisible  = this.scorePanel.activeSelf;

				this.mainPanelImage.enabled = false;
				this.targetsOfHiding.SetActive(false);
				this.giveUpPanel.SetActive(false);
				this.scorePanel.SetActive(false);
			}
			else
			{
				if (this.isMainPanelVisible)
				{
					this.mainPanelImage.enabled = true;
					this.targetsOfHiding.SetActive(true);
				}
				if (this.isGiveUpPanelVisible)
				{
					this.giveUpPanel.SetActive(true);
				}
				if (this.isScorePanelVisible)
				{
					this.scorePanel.SetActive(true);
				}
			}
		}

		private void UpdateAudienceCameraDepth()
		{
			for (int i = 0; i < this.audienceCameras.Count; i++)
			{
				if (i == mainAudienceCameraNo)
				{
					this.audienceCameras[i].depth = 10;
				}
				else
				{
					this.audienceCameras[i].depth = 0;
				}
			}
		}

		public void ClickCameraButton()
		{
			this.mainAudienceCameraNo++;

			if(mainAudienceCameraNo >= audienceCameras.Count)
			{
				mainAudienceCameraNo = 0;
			}

			this.UpdateAudienceCameraDepth();
		}

		public void ClickGiveUpButton()
		{
			if (this.giveUpPanel.activeSelf)
			{
				this.giveUpPanel.SetActive(false);
			}
			else
			{
				this.giveUpPanel.SetActive(true);
			}
		}

		//public void ClickStartButton()
		//{
		//	Debug.Log("ClickStartButton");
		//}

		public void ClickGiveUpYesButton()
		{
			this.moderator.InterruptGiveUp();
		}

		public void ClickGiveUpNoButton()
		{
			if (this.giveUpPanel.activeSelf)
			{
				this.giveUpPanel.SetActive(false);
			}
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
			if (eventData.pointerEnter == null) { return; }

			Transform selectedObj = eventData.pointerEnter.transform;

			do
			{
				if (selectedObj.gameObject.GetInstanceID() == this.mainPanel.GetInstanceID() ||
					selectedObj.gameObject.GetInstanceID() == this.scorePanel.GetInstanceID() ||
					selectedObj.gameObject.GetInstanceID() == this.giveUpPanel.GetInstanceID())
				{
					this.draggingPanel = selectedObj.gameObject;
					break;
				}

				selectedObj = selectedObj.transform.parent;

			} while (selectedObj.transform.parent != null);
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (this.draggingPanel == null) { return; }

			this.draggingPanel.transform.position += (Vector3)eventData.delta;
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			this.draggingPanel = null;
		}

		public Transform GetAudienceCameraTarnsform()
		{
			return audienceCameras[mainAudienceCameraNo].transform;
		}
	}
}
