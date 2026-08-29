namespace DeepLearning.Api.Constants
{
    public static class ApiRoutes
    {
        public const string Prefix = "api/v1";

        public static class ExamTypes
        {
            public const string Base = $"{Prefix}/exam-types";
        }

        public static class AssessmentDimensions
        {
            public const string Base = $"{Prefix}/exam-types/{{examTypeId:guid}}/assessment-dimensions";
        }

        public static class ErrorTaxonomies
        {
            public const string Base = $"{Prefix}/exam-types/{{examTypeId:guid}}/error-taxonomies";
        }

        public static class PromptTemplates
        {
            public const string Base = $"{Prefix}/prompt-templates";
        }

        public static class Users
        {
            public const string Base = $"{Prefix}/users";
        }
    }
}
