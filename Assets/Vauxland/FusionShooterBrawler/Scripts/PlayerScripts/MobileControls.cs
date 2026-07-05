/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vauxland.FusionBrawler
{
    public class MobileControls : MonoBehaviour
    {
        public Joystick movementJoystick;
        public Joystick aimJoystick;
        public Button shootButton;
        public Button jumpButton;

        [HideInInspector] public bool isShooting;
        [HideInInspector] public bool useMobileControls = false;

        [SerializeField] private PlayerInputManager playerInputManager;
        [SerializeField] private PlayerManager _playerManager;

        void Start()
        {
            // attach button listeners
            if (jumpButton != null)
                jumpButton.onClick.AddListener(OnJumpButtonDown);
        }

        void Update()
        {
            if (_playerManager.Object.HasInputAuthority == false)
                return;

            if (useMobileControls)
                HandleInput();
        }

        // controls moving the player when using the mobile controls
        void HandleInput()
        {
            Vector3 movement = new Vector3(movementJoystick.Horizontal(), 0, movementJoystick.Vertical());
            Vector3 aim = new Vector3(aimJoystick.Horizontal(), 0, aimJoystick.Vertical());

            playerInputManager.SetMovementInput(movement);

            if (aim.magnitude > 0.1f)
            {
                playerInputManager.SetAimInput(aim);
            }
            else
            {
                playerInputManager.SetAimInput(movement);
            }
        }

        public void SetUpShootButtonEventTriggers()
        {
            EventTrigger trigger = shootButton.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = shootButton.gameObject.AddComponent<EventTrigger>();
            }

            // add PointerDown event
            EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry();
            pointerDownEntry.eventID = EventTriggerType.PointerDown;
            pointerDownEntry.callback.AddListener((data) => { OnShootButtonDown(); });
            trigger.triggers.Add(pointerDownEntry);

            // add PointerUp event
            EventTrigger.Entry pointerUpEntry = new EventTrigger.Entry();
            pointerUpEntry.eventID = EventTriggerType.PointerUp;
            pointerUpEntry.callback.AddListener((data) => { OnShootButtonUp(); });
            trigger.triggers.Add(pointerUpEntry);
        }
        
        void OnJumpButtonDown()
        {
            playerInputManager.SetJumpInput(true);
        }

        // when shoot button is pressed
        public void OnShootButtonDown()
        {
            isShooting = true;
        }

        // when shoot button is released
        public void OnShootButtonUp()
        {
            isShooting = false;
        }
    }
}


