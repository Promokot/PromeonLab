using System;

[Serializable]
public struct ControlBinding
{
    public ControlBindingCategory Category;
    public string                 Action;       // короткое название, напр. "Move"
    public string                 Description;  // пояснение для строки
    public ControlHand            Hand;
    public string                 InputLabel;   // напр. "stick", "tap trigger", "X / A"
    public string                 IconId;       // опц. id иконки (для md/будущего UI); может быть пустым
}
