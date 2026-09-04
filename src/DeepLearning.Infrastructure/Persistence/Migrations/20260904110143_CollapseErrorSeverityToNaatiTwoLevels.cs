using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Collapses error_severity_enum from the four invented levels to NAATI's own two.
    ///
    /// <para>minor / moderate / major / critical were subdivisions of the official Major and
    /// Minor, and they earned nothing: critical required three conditions at once and proved
    /// unreachable, while moderate absorbed everything else. Across 55 real findings the split
    /// was 40 moderate, 14 major, 1 critical and zero minor — a four-point scale being used as a
    /// two-point one, with neither point where the rubric puts it.</para>
    ///
    /// <para>Existing rows are mapped by the definition they were subdivisions of: moderate was
    /// an officially-Minor error that still cost propositional content, and critical was an
    /// officially-Major one with reach past its own sentence.</para>
    ///
    /// <para>Written by hand: PostgreSQL cannot remove a label from an enum type, so the column
    /// is retyped onto a fresh two-label type and the old one dropped. EF generated an empty
    /// migration because it does not diff enum labels.</para>
    /// </summary>
    public partial class CollapseErrorSeverityToNaatiTwoLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE error_list ALTER COLUMN severity DROP DEFAULT;

                UPDATE error_list SET severity = 'minor' WHERE severity = 'moderate';
                UPDATE error_list SET severity = 'major' WHERE severity = 'critical';

                CREATE TYPE error_severity_enum_new AS ENUM ('minor', 'major');
                ALTER TABLE error_list
                    ALTER COLUMN severity TYPE error_severity_enum_new
                    USING severity::text::error_severity_enum_new;
                DROP TYPE error_severity_enum;
                ALTER TYPE error_severity_enum_new RENAME TO error_severity_enum;

                ALTER TABLE error_list ALTER COLUMN severity SET DEFAULT 'minor';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The two intermediate levels cannot come back — nothing recorded which minor rows
            // used to be moderate. This restores the type, not the lost distinction.
            migrationBuilder.Sql("""
                ALTER TABLE error_list ALTER COLUMN severity DROP DEFAULT;

                CREATE TYPE error_severity_enum_old AS ENUM ('critical', 'major', 'minor', 'moderate');
                ALTER TABLE error_list
                    ALTER COLUMN severity TYPE error_severity_enum_old
                    USING severity::text::error_severity_enum_old;
                DROP TYPE error_severity_enum;
                ALTER TYPE error_severity_enum_old RENAME TO error_severity_enum;

                ALTER TABLE error_list ALTER COLUMN severity SET DEFAULT 'moderate';
                """);
        }
    }
}
