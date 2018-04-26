using SIGVerse.Competition.Handyman;
using SIGVerse.Human;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityStandardAssets_1_1_2.Characters.ThirdPerson;
using UnityStandardAssets_1_1_2.CrossPlatformInput;

public class HandymanHumanAvatarController : MonoBehaviour
{
	public enum ChangeoverKey{ Key1, Key2 }

	public ChangeoverKey changeoverKey = ChangeoverKey.Key1;

	public float walk_speed =  0.2f;

	//------------------------------------------------
	private bool isControlling = true;

	private Animator              animator;
	private Rigidbody             rootRigidbody;
	private CapsuleCollider       rootCapsuleCollider;
	private List<CapsuleCollider> capsuleColliders;
	private ThirdPersonCharacter  thirdPersonCharacter;

	private bool shouldMove = false;

	
	private void Awake()
	{
		this.animator              = this.GetComponent<Animator>();
		this.rootRigidbody         = this.GetComponent<Rigidbody>();
		this.rootCapsuleCollider   = this.GetComponent<CapsuleCollider>();
		this.capsuleColliders      = this.GetComponentsInChildren<CapsuleCollider>().ToList<CapsuleCollider>();
		this.thirdPersonCharacter  = this.GetComponent<ThirdPersonCharacter>();

		this.capsuleColliders.Remove(this.rootCapsuleCollider);

		if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
		{
			this.animator.enabled = true;
			this.rootRigidbody.useGravity = true;
			this.rootRigidbody.isKinematic = false;
			this.rootCapsuleCollider.enabled = true;
			foreach(CapsuleCollider capsuleCollider in this.capsuleColliders){ capsuleCollider.enabled = false; }
			this.thirdPersonCharacter.enabled = true;
		}
		else
		{
			this.animator.enabled = false;
			this.rootRigidbody.useGravity = false;
			this.rootRigidbody.isKinematic = true;
			this.rootCapsuleCollider.enabled = false;
			foreach(CapsuleCollider capsuleCollider in this.capsuleColliders){ capsuleCollider.enabled = true; }
			this.thirdPersonCharacter.enabled = false;

			this.isControlling = false;
		}
	}


	private void Update()
	{
		if(!this.isControlling) { return; }

		if(EventSystem.current!=null && EventSystem.current.currentSelectedGameObject!=null) { return; }

		bool hasPress0 = Input.GetKey(KeyCode.Alpha0) || Input.GetKey(KeyCode.Keypad0); // Disable operation
		bool hasPress1 = Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Keypad1);
		bool hasPress2 = Input.GetKey(KeyCode.Alpha2) || Input.GetKey(KeyCode.Keypad2);

		switch(this.changeoverKey)
		{
			case ChangeoverKey.Key1:
			{
				if(hasPress1) { this.shouldMove = true; }
				if(hasPress0 || hasPress2) { this.shouldMove = false; }
				break;
			}
			case ChangeoverKey.Key2:
			{
				if(hasPress2) { this.shouldMove = true; }
				if(hasPress0 || hasPress1) { this.shouldMove = false; }
				break;
			}
		}

		if(this.shouldMove) { this.MoveCharacter(); }
	}

	private void MoveCharacter()
	{
		// read inputs
		float h = CrossPlatformInputManager.GetAxis("Horizontal") * walk_speed;
		float v = CrossPlatformInputManager.GetAxis("Vertical")   * walk_speed;

		Vector3 m_Move = v * Vector3.forward + h * Vector3.right;

		this.thirdPersonCharacter.Move(m_Move, false, false);
	}
}
