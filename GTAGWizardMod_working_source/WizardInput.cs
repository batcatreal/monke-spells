
using UnityEngine.XR;

namespace GTAGWizard.Controls
{
    internal sealed class ButtonTracker
    {
        public bool IsDown { get; private set; }
        public bool PressedThisFrame { get; private set; }

        public void Update(bool isDownNow)
        {
            PressedThisFrame = isDownNow && !IsDown;
            IsDown = isDownNow;
        }
    }

    internal sealed class WizardInput
    {
        private readonly ButtonTracker _trigger = new ButtonTracker();
        private readonly ButtonTracker _grip = new ButtonTracker();
        private readonly ButtonTracker _a = new ButtonTracker();

        public bool GripHeld => _grip.IsDown;
        public bool APressed => _a.PressedThisFrame;
        public bool TriggerPressed => _trigger.PressedThisFrame;

        public void Poll()
        {
            ControllerInputPoller poller = ControllerInputPoller.instance;

            // Wait until Gorilla Tag's input system is available.
            if (poller == null)
            {
                _grip.Update(false);
                _a.Update(false);
                _trigger.Update(false);
                return;
            }

            bool grip = ControllerInputPoller.GetGrab(XRNode.RightHand);

            bool a = ControllerInputPoller.PrimaryButtonPress(
                XRNode.RightHand);

            bool trigger = poller.rightControllerTriggerButton;

            _grip.Update(grip);
            _a.Update(a);
            _trigger.Update(trigger);
        }
    }
}
