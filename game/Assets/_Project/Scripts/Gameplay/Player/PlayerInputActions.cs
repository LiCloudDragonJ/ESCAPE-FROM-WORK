using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeFromWork.Gameplay
{
    /// Builds the player action map in code so M0 does not need an input asset wired by hand.
    /// Bindings are rebindable at runtime and persisted in PlayerPrefs.
    public sealed class PlayerInputActions : IDisposable
    {
        const string OverrideKey = "efw.input.overrides";

        public InputActionAsset Asset { get; }
        public InputActionMap Map { get; }
        public InputAction Move { get; }
        public InputAction Look { get; }
        public InputAction Jump { get; }
        public InputAction Sprint { get; }
        public InputAction Crouch { get; }

        public PlayerInputActions()
        {
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Asset.name = "PlayerControls";

            Map = new InputActionMap("Player");
            Asset.AddActionMap(Map);

            Move = Map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");

            Look = Map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            Look.AddBinding("<Mouse>/delta");
            Look.AddBinding("<Gamepad>/rightStick", processors: "scaleVector2(x=120,y=120)");

            Jump = Map.AddAction("Jump", InputActionType.Button);
            Jump.AddBinding("<Keyboard>/space");
            Jump.AddBinding("<Gamepad>/buttonSouth");

            Sprint = Map.AddAction("Sprint", InputActionType.Button);
            Sprint.AddBinding("<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Crouch = Map.AddAction("Crouch", InputActionType.Button);
            Crouch.AddBinding("<Keyboard>/leftCtrl");
            Crouch.AddBinding("<Gamepad>/buttonEast");
        }

        public void Enable() => Map.Enable();

        public void Disable() => Map.Disable();

        public InputAction Find(string actionName) => Map.FindAction(actionName);

        /// Starts an interactive rebind; the callback fires on complete or cancel.
        public InputActionRebindingExtensions.RebindingOperation Rebind(string actionName, int bindingIndex, Action onFinished = null)
        {
            var action = Find(actionName);
            if (action == null) throw new ArgumentException("Unknown action: " + actionName, nameof(actionName));
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) throw new ArgumentOutOfRangeException(nameof(bindingIndex));

            return action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op => { op.Dispose(); SaveOverrides(); onFinished?.Invoke(); })
                .OnCancel(op => { op.Dispose(); onFinished?.Invoke(); })
                .Start();
        }

        public void SaveOverrides()
        {
            PlayerPrefs.SetString(OverrideKey, Asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public void LoadOverrides()
        {
            string json = PlayerPrefs.GetString(OverrideKey, string.Empty);
            if (!string.IsNullOrEmpty(json)) Asset.LoadBindingOverridesFromJson(json);
        }

        public void ResetOverrides()
        {
            Asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(OverrideKey);
        }

        public void Dispose()
        {
            Map.Disable();
            UnityEngine.Object.Destroy(Asset);
        }
    }
}
