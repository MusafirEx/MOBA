using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vauxland.FusionBrawler
{
    public class PlayerKillPopup : MonoBehaviour
    {
        public TextMeshProUGUI attackerNameText; // attacking players name will show in this text
        public TextMeshProUGUI victimNameText; // victim players name will show in this text
        public TextMeshProUGUI weaponNameText; // the name of the weapon will show in this text
        public Image weaponIcon; // the weapons icon will show here
        public GameObject teamRedImage; // if its a red team kill
        public GameObject teamBlueImage; // if its a blue team kill
    }
}

