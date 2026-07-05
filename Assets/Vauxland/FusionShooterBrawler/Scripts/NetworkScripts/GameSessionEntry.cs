/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using Fusion;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vauxland.FusionBrawler
{
    public class GameSessionEntry : MonoBehaviour
    {
        public TextMeshProUGUI gameNameText; // displays the name of the session
        public TextMeshProUGUI gameModeText; // displays the match's match type
        public TextMeshProUGUI gameMapText; // displays the match's map name
        public TextMeshProUGUI playerCountText; // displays the current players count
        public Button joinButton; // the button to join the session

        [HideInInspector] public MatchType matchType;
        private string mapName;

        private SessionInfo sessionInfo;

        public event Action<SessionInfo> OnJoinSession;

        public void SetSessionInfo(SessionInfo sessionInfo)
        {
            // set our session info
            this.sessionInfo = sessionInfo;
            gameNameText.text = sessionInfo.Name;

            if (sessionInfo.Properties != null)
            {
                if (sessionInfo.Properties.TryGetValue("MatchType", out var matchTypeProperty)) // gets the match type from the session info
                {
                    matchType = (MatchType)(int)matchTypeProperty.PropertyValue;
                }

                if (sessionInfo.Properties.TryGetValue("MapName", out var mapNameProperty)) // gets the map name from the session info
                {
                    mapName = (string)mapNameProperty.PropertyValue;
                }
            }

            gameModeText.text = matchType.ToString();
            gameMapText.text = mapName.ToString();
            playerCountText.text = $"{sessionInfo.PlayerCount.ToString()}/{sessionInfo.MaxPlayers.ToString()}";

            bool isJoinable = sessionInfo.PlayerCount < sessionInfo.MaxPlayers; 

            joinButton.gameObject.SetActive(isJoinable); // if match is full can't join
        }

        // joins the session
        public void OnClickJoin()
        {
            MainMenuManager mainMenu = FindObjectOfType<MainMenuManager>();
            if (mainMenu != null)
            {
                mainMenu.lobbyUI.SetActive(false);
                mainMenu.joinGamePanel.SetActive(true);
                mainMenu.loadingGameText.text = "Joining Match";
            }
            OnJoinSession?.Invoke(sessionInfo);
        }
    }
}

