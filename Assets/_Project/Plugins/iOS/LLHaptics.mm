// LOST & LOVELY — iOS haptics (FX-05, SPEC4 §13.5 g). Called from IdleGame.Platform.IosHapticsBackend via DllImport("__Internal").
// style: 0 = Light, 1 = Medium, 2 = Heavy (IdleGame.HapticKind). One cached UIImpactFeedbackGenerator per style, prepared on creation
// and again after each impact so the next one fires with minimal latency. Runs on the Unity main thread (the UIKit main thread on iOS).
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *LLHapticsGenerators[3];

static UIImpactFeedbackGenerator *LLHapticsGenerator(int style)
{
    if (style < 0 || style > 2) style = 0;
    if (LLHapticsGenerators[style] == nil)
    {
        UIImpactFeedbackStyle feedback = UIImpactFeedbackStyleLight;
        if (style == 1) feedback = UIImpactFeedbackStyleMedium;
        else if (style == 2) feedback = UIImpactFeedbackStyleHeavy;
        LLHapticsGenerators[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:feedback];
        [LLHapticsGenerators[style] prepare];
    }
    return LLHapticsGenerators[style];
}

extern "C" void LLHaptics_Impact(int style)
{
    UIImpactFeedbackGenerator *generator = LLHapticsGenerator(style);
    [generator impactOccurred];
    [generator prepare];
}
