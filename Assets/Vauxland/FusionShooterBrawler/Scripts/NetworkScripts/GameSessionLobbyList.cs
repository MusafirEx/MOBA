/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using UnityEngine;
using TMPro;
using Fusion;

namespace Vauxland.FusionBrawler
{
    public class GameSessionLobbyList : MonoBehaviour
    {
        public TextMeshProUGUI loadingText; // displays searching for a match
        [SerializeField] private GameSessionEntry _sessionEntryPrefab = null; // the ui prefab to spawn in the list of game sessions to represent the game session
        [SerializeField] public GameObject sessionEntryListContainer; // the transform where new game session entries will spawn
        [SerializeField] private MainMenuManager mainMenu; // main menu in the scene to turn off when joining a match.

        private void Awake()
        {
            ClearContainerList();
        }

        public void ClearContainerList()
        {
            foreach (Transform child in sessionEntryListContainer.transform)
            {
                Destroy(child.gameObject);
            }
        }

        // adds a created game session to the list so players can join it
        public void AddGameSession(SessionInfo sessionInfo)
        {
            loadingText.gameObject.SetActive(false);

            GameSessionEntry sessionEntry = null;

            sessionEntry = Instantiate(_sessionEntryPrefab, sessionEntryListContainer.transform);

            sessionEntry.SetSessionInfo(sessionInfo);

            sessionEntry.OnJoinSession += SessionEntry_OnJoinSession;
        }

        // tells the runner manager we are joinging this session
        private void SessionEntry_OnJoinSession(SessionInfo sessionInfo)
        {
            mainMenu.runnerManager.JoinGame(sessionInfo);
        }

        // changes the loading text to let user know no matches found
        public void NoGameSessionFound()
        {
            ClearContainerList();
            loadingText.text = "No Matches found";
        }

        // changes the laoding text to let user know its searching for a match
        public void LookingForGameSession()
        {
            ClearContainerList();
            loadingText.text = "Searching for a Match..";
        }
    }
}

