namespace DeepLearning.Domain.Enums
{
    /// <summary>
    /// 成员名用PascalCase(New/Familiar/Mastered),经snake_case转换后对应
    /// mastery_level_enum的label('new'等);'new'是C#保留字,不能直接当枚举成员名。
    /// </summary>
    public enum MasteryLevel
    {
        New,
        Familiar,
        Mastered
    }
}
