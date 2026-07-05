/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Vauxland.FusionBrawler
{
    public class Joystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
    {
        public Image backgroundImage;
        public Image joystickImage;

        private Vector2 inputVector;

        // adjust this factor to control how quickly the joystick reaches full speed.
        // a higher value decreases the drag effect and a lower value increases the drag effect.
        public float dragFactor = 1f;


        public float Horizontal()
        {
            return inputVector.x;
        }

        public float Vertical()
        {
            return inputVector.y;
        }

        public Vector2 Direction()
        {
            return new Vector2(Horizontal(), Vertical());
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 pos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(backgroundImage.rectTransform, eventData.position, eventData.pressEventCamera, out pos))
            {
                pos.x = (pos.x / backgroundImage.rectTransform.sizeDelta.x) * 2;
                pos.y = (pos.y / backgroundImage.rectTransform.sizeDelta.y) * 2;

                // create a raw input vector
                Vector2 rawInput = new Vector2(pos.x, pos.y);

                // apply the drag factor to control how quickly the input reaches full speed.
                inputVector = rawInput * dragFactor;

                // clamp the vector to make sure it doesn't exceed the bounds.
                inputVector = (inputVector.magnitude > 1f) ? inputVector = rawInput.normalized : inputVector;

                // move the joystick image based on the calculated input vector.
                joystickImage.rectTransform.anchoredPosition = new Vector2(inputVector.x * (backgroundImage.rectTransform.sizeDelta.x / 2), inputVector.y * (backgroundImage.rectTransform.sizeDelta.y / 2));

            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            inputVector = Vector2.zero;
            joystickImage.rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}


