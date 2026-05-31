#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

extern "C" void PawPalShareImage(const char *imagePath, const char *title, const char *message)
{
    NSString *path = imagePath != NULL ? [NSString stringWithUTF8String:imagePath] : @"";
    NSString *subject = title != NULL ? [NSString stringWithUTF8String:title] : @"PawFriends Photo";
    NSString *body = message != NULL ? [NSString stringWithUTF8String:message] : @"";

    dispatch_async(dispatch_get_main_queue(), ^{
        NSURL *fileURL = [NSURL fileURLWithPath:path];
        NSMutableArray *items = [NSMutableArray array];
        if (body.length > 0)
        {
            [items addObject:body];
        }

        if (fileURL != nil)
        {
            [items addObject:fileURL];
        }

        if (items.count == 0)
        {
            return;
        }

        UIActivityViewController *controller = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        [controller setValue:subject forKey:@"subject"];

        UIViewController *root = UIApplication.sharedApplication.keyWindow.rootViewController;
        while (root.presentedViewController != nil)
        {
            root = root.presentedViewController;
        }

        if (controller.popoverPresentationController != nil)
        {
            controller.popoverPresentationController.sourceView = root.view;
            controller.popoverPresentationController.sourceRect = CGRectMake(root.view.bounds.size.width * 0.5f, root.view.bounds.size.height, 1.0f, 1.0f);
        }

        [root presentViewController:controller animated:YES completion:nil];
    });
}
