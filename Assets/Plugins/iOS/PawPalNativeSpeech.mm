#import <AVFoundation/AVFoundation.h>
#import <Foundation/Foundation.h>
#import <Speech/Speech.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);
extern "C" void PawPalVoice_StartListening(void);

static NSString *PawPalVoiceCallbackTarget = nil;
static NSArray<NSString *> *PawPalVoiceContextualPhrases = @[];
static AVAudioEngine *PawPalVoiceAudioEngine = nil;
static SFSpeechRecognizer *PawPalVoiceRecognizer = nil;
static SFSpeechAudioBufferRecognitionRequest *PawPalVoiceRecognitionRequest = nil;
static SFSpeechRecognitionTask *PawPalVoiceRecognitionTask = nil;
static BOOL PawPalVoiceShouldListen = NO;

static void PawPalVoiceSendMessage(NSString *method, NSString *message)
{
    if (PawPalVoiceCallbackTarget == nil || PawPalVoiceCallbackTarget.length == 0 || method == nil)
    {
        return;
    }

    const char *target = PawPalVoiceCallbackTarget.UTF8String;
    const char *methodName = method.UTF8String;
    const char *payload = message != nil ? message.UTF8String : "";
    UnitySendMessage(target, methodName, payload);
}

static void PawPalVoiceSendFailure(NSString *reason, NSString *message)
{
    NSString *payload = [NSString stringWithFormat:@"%@|%@", reason != nil ? reason : @"Unsupported", message != nil ? message : @"Voice recognition failed."];
    PawPalVoiceSendMessage(@"OnPawPalIosVoiceFailure", payload);
}

static void PawPalVoiceStopActiveSession(BOOL releaseEngine)
{
    if (PawPalVoiceAudioEngine != nil && PawPalVoiceAudioEngine.isRunning)
    {
        [PawPalVoiceAudioEngine stop];
    }

    if (PawPalVoiceAudioEngine != nil)
    {
        AVAudioInputNode *inputNode = PawPalVoiceAudioEngine.inputNode;
        [inputNode removeTapOnBus:0];
    }

    [PawPalVoiceRecognitionRequest endAudio];
    [PawPalVoiceRecognitionTask cancel];
    PawPalVoiceRecognitionRequest = nil;
    PawPalVoiceRecognitionTask = nil;

    if (releaseEngine)
    {
        PawPalVoiceAudioEngine = nil;
    }
}

static void PawPalVoiceRestartIfNeeded(void)
{
    if (!PawPalVoiceShouldListen)
    {
        return;
    }

    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        if (!PawPalVoiceShouldListen)
        {
            return;
        }

        PawPalVoice_StartListening();
    });
}

extern "C" bool PawPalVoice_IsSpeechRecognitionAvailable()
{
    if (@available(iOS 10.0, *))
    {
        return YES;
    }

    return NO;
}

extern "C" void PawPalVoice_SetCallbackTarget(const char *gameObjectName)
{
    if (gameObjectName == NULL)
    {
        PawPalVoiceCallbackTarget = nil;
        return;
    }

    PawPalVoiceCallbackTarget = [NSString stringWithUTF8String:gameObjectName];
}

extern "C" void PawPalVoice_SetContextualPhrases(const char *joinedPhrases)
{
    if (joinedPhrases == NULL)
    {
        PawPalVoiceContextualPhrases = @[];
        return;
    }

    NSString *rawValue = [NSString stringWithUTF8String:joinedPhrases];
    if (rawValue.length == 0)
    {
        PawPalVoiceContextualPhrases = @[];
        return;
    }

    NSArray<NSString *> *parts = [rawValue componentsSeparatedByString:@"\n"];
    NSMutableArray<NSString *> *cleanParts = [NSMutableArray arrayWithCapacity:parts.count];
    for (NSString *part in parts)
    {
        NSString *trimmed = [part stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
        if (trimmed.length > 0)
        {
            [cleanParts addObject:trimmed];
        }
    }

    PawPalVoiceContextualPhrases = cleanParts;
}

extern "C" void PawPalVoice_RequestSpeechPermission()
{
    if (@available(iOS 10.0, *))
    {
        [SFSpeechRecognizer requestAuthorization:^(SFSpeechRecognizerAuthorizationStatus status) {
            dispatch_async(dispatch_get_main_queue(), ^{
                switch (status)
                {
                    case SFSpeechRecognizerAuthorizationStatusAuthorized:
                        PawPalVoiceSendMessage(@"OnPawPalIosVoicePermission", @"granted");
                        break;
                    default:
                        PawPalVoiceSendMessage(@"OnPawPalIosVoicePermission", @"denied");
                        break;
                }
            });
        }];
        return;
    }

    PawPalVoiceSendMessage(@"OnPawPalIosVoicePermission", @"denied");
}

extern "C" void PawPalVoice_StopListening()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        PawPalVoiceShouldListen = NO;
        PawPalVoiceStopActiveSession(NO);
    });
}

extern "C" void PawPalVoice_StartListening()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (!@available(iOS 10.0, *))
        {
            PawPalVoiceSendFailure(@"Unsupported", @"Speech recognition is unavailable on this iOS version.");
            return;
        }

        PawPalVoiceShouldListen = YES;
        PawPalVoiceStopActiveSession(NO);

        if (PawPalVoiceRecognizer == nil)
        {
            NSLocale *locale = [NSLocale currentLocale];
            PawPalVoiceRecognizer = [[SFSpeechRecognizer alloc] initWithLocale:locale];
        }

        if (PawPalVoiceRecognizer == nil || !PawPalVoiceRecognizer.available)
        {
            PawPalVoiceSendFailure(@"Unsupported", @"Speech recognition is unavailable right now.");
            return;
        }

        NSError *audioSessionError = nil;
        AVAudioSession *audioSession = [AVAudioSession sharedInstance];
        [audioSession setCategory:AVAudioSessionCategoryPlayAndRecord
                             mode:AVAudioSessionModeMeasurement
                          options:AVAudioSessionCategoryOptionDuckOthers
                            error:&audioSessionError];
        [audioSession setActive:YES withOptions:AVAudioSessionSetActiveOptionNotifyOthersOnDeactivation error:&audioSessionError];
        if (audioSessionError != nil)
        {
            PawPalVoiceSendFailure(@"NoMicrophone", @"Microphone audio session could not start.");
            return;
        }

        PawPalVoiceAudioEngine = [[AVAudioEngine alloc] init];
        PawPalVoiceRecognitionRequest = [[SFSpeechAudioBufferRecognitionRequest alloc] init];
        PawPalVoiceRecognitionRequest.shouldReportPartialResults = NO;
        if (PawPalVoiceContextualPhrases != nil && PawPalVoiceContextualPhrases.count > 0)
        {
            PawPalVoiceRecognitionRequest.contextualStrings = PawPalVoiceContextualPhrases;
        }

        AVAudioInputNode *inputNode = PawPalVoiceAudioEngine.inputNode;
        if (inputNode == nil)
        {
            PawPalVoiceSendFailure(@"NoMicrophone", @"No microphone input node was found.");
            return;
        }

        __block BOOL deliveredFinalResult = NO;
        PawPalVoiceRecognitionTask = [PawPalVoiceRecognizer recognitionTaskWithRequest:PawPalVoiceRecognitionRequest
                                                                        resultHandler:^(SFSpeechRecognitionResult * _Nullable result, NSError * _Nullable error) {
            if (result != nil && result.bestTranscription.formattedString.length > 0 && result.isFinal && !deliveredFinalResult)
            {
                deliveredFinalResult = YES;
                PawPalVoiceSendMessage(@"OnPawPalIosVoiceRecognized", result.bestTranscription.formattedString);
            }

            if (error != nil)
            {
                NSString *message = error.localizedDescription != nil ? error.localizedDescription : @"Speech recognition stopped.";
                PawPalVoiceSendFailure(@"UnclearCommand", message);
                PawPalVoiceStopActiveSession(NO);
                PawPalVoiceRestartIfNeeded();
                return;
            }

            if (result != nil && result.isFinal)
            {
                PawPalVoiceStopActiveSession(NO);
                PawPalVoiceRestartIfNeeded();
            }
        }];

        AVAudioFormat *format = [inputNode outputFormatForBus:0];
        [inputNode removeTapOnBus:0];
        [inputNode installTapOnBus:0
                        bufferSize:1024
                            format:format
                             block:^(AVAudioPCMBuffer *buffer, AVAudioTime *when) {
            if (PawPalVoiceRecognitionRequest != nil)
            {
                [PawPalVoiceRecognitionRequest appendAudioPCMBuffer:buffer];
            }
        }];

        NSError *engineError = nil;
        [PawPalVoiceAudioEngine prepare];
        [PawPalVoiceAudioEngine startAndReturnError:&engineError];
        if (engineError != nil)
        {
            PawPalVoiceSendFailure(@"NoMicrophone", engineError.localizedDescription != nil ? engineError.localizedDescription : @"Microphone capture could not start.");
            PawPalVoiceStopActiveSession(NO);
            return;
        }
    });
}
