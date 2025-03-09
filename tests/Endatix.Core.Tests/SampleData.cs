namespace Endatix.Core.Tests;

public static class SampleData
{
    public const long TENANT_ID = 1;
    public const string FORM_NAME_1 = "Product Form";
    public const string FORM_NAME_2 = "Insurance Form";
    public const string FORM_DESCRIPTION_1 = "Product Form Description";
    public const string FORM_DESCRIPTION_2 = "Insurance Form Description";
    public const string FORM_DEFINITION_JSON_DATA_1 = "{\"type\":\"text\",\"name\":\"firstname\"";
    public const string FORM_DEFINITION_JSON_DATA_2 = "{\"type\":\"text\",\"name\":\"lastname\"";
    public const string SUBMISSION_JSON_DATA_1 = "{\"firstname\":\"mr test\"";
    public const string SUBMISSION_JSON_DATA_2 = "{\"firstname\":\"mrs test\"";

    public class SubmissionJsons
    {

        public const string MR_TEST = "{\"firstname\":\"mr test\"";
        public const string MRS_TEST = "{\"firstname\":\"mrs test\"";

        public const string MISSING_FEAT_1 = "{\"missing-feature\":\"robot\"";

        public const string MISSING_FEAT_2 = "{\"missing-feature\":\"rocket\"";
    }

    public class IDs
    {
        public const long ID_121 = 121;
        public const long ID_122 = 122;
        public const long ID_123 = 123;
        public const long ID_124 = 124;
        public const long ID_125 = 125;
        public const long ID_126 = 126;
        public const long ID_127 = 127;
        public const long ID_128 = 128;
        public const long ID_129 = 129;
        public const long ID_130 = 130;
    }
}
