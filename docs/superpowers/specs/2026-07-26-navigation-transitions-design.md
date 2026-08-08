# Navigation transitions design

The primary navigation supports Text, Icon, and IconAndText modes. Every transition
between those modes cross-fades the icon and label while sliding newly visible
content into place. Rapid changes replace the active animation.

Selecting another primary page fades and slightly shifts the current page out,
then fades and shifts the destination page in. The settings tab navigation uses
the same principle: the selected secondary item receives a short emphasis
transition and the complete right-hand settings content fades out and back in.

Animations honor `SystemParameters.ClientAreaAnimation`; when Windows disables
client animations, state changes are immediate. Existing commands, keyboard
navigation, drag reordering, settings persistence, and page bindings remain intact.
