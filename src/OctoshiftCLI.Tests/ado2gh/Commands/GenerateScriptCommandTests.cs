using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.Extensions;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class GenerateScriptCommandTests
    {
        private const string ADO_ORG = "ADO_ORG";
        private const string ADO_TEAM_PROJECT = "ADO_TEAM_PROJECT";
        private const string FOO_REPO = "FOO_REPO";
        private const string FOO_PIPELINE = "FOO_PIPELINE";
        private const string BAR_REPO = "BAR_REPO";
        private const string BAR_PIPELINE = "BAR_PIPELINE";
        private const string APP_ID = "d9edf292-c6fd-4440-af2b-d08fcc9c9dd1";
        private const string GITHUB_ORG = "GITHUB_ORG";

        private readonly IEnumerable<string> ADO_ORGS = new List<string>() { ADO_ORG };
        private readonly IDictionary<string, IEnumerable<string>> ADO_TEAM_PROJECTS = new Dictionary<string, IEnumerable<string>>() { { ADO_ORG, new List<string>() { ADO_TEAM_PROJECT } } };
        private readonly IDictionary<string, IDictionary<string, IEnumerable<string>>> ADO_REPOS = new Dictionary<string, IDictionary<string, IEnumerable<string>>>() { { ADO_ORG, new Dictionary<string, IEnumerable<string>>() { { ADO_TEAM_PROJECT, new List<string>() { FOO_REPO } } } } };
        private readonly IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> ADO_PIPELINES =
            new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>()
            { { ADO_ORG, new Dictionary<string, IDictionary<string, IEnumerable<string>>>()
                         { { ADO_TEAM_PROJECT, new Dictionary<string, IEnumerable<string>>()
                                               { { FOO_REPO, new List<string>()
                                                             { FOO_PIPELINE } } } } } } };

        private readonly IEnumerable<string> EMPTY_ORGS = new List<string>();
        private readonly IDictionary<string, IEnumerable<string>> EMPTY_TEAM_PROJECTS = new Dictionary<string, IEnumerable<string>>();
        private readonly IDictionary<string, IDictionary<string, IEnumerable<string>>> EMPTY_REPOS = new Dictionary<string, IDictionary<string, IEnumerable<string>>>();
        private readonly IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> EMPTY_PIPELINES = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>();

        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<AdoInspectorService> _mockAdoInspector = TestHelpers.CreateMock<AdoInspectorService>();

        private string _scriptOutput = "";
        private readonly GenerateScriptCommand _command;

        public GenerateScriptCommandTests()
        {
            var mockVersionProvider = new Mock<IVersionProvider>();
            mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            _command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, _mockAdoApiFactory.Object, mockVersionProvider.Object, _mockAdoInspector.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    _scriptOutput = contents;
                    return Task.CompletedTask;
                }
            };
        }

        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateScriptCommand(null, null, null, null);
            command.Should().NotBeNull();
            command.Name.Should().Be("generate-script");
            command.Options.Count.Should().Be(15);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", false);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "sequential", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(command.Options, "create-teams", false);
            TestHelpers.VerifyCommandOption(command.Options, "link-idp-groups", false);
            TestHelpers.VerifyCommandOption(command.Options, "lock-ado-repos", false);
            TestHelpers.VerifyCommandOption(command.Options, "disable-ado-repos", false);
            TestHelpers.VerifyCommandOption(command.Options, "integrate-boards", false);
            TestHelpers.VerifyCommandOption(command.Options, "rewire-pipelines", false);
            TestHelpers.VerifyCommandOption(command.Options, "all", false);
        }

        [Fact]
        public async Task SequentialScript_No_Data()
        {
            // Arrange
            var orgs = new List<string>();
            var teamProjects = new Dictionary<string, IEnumerable<string>>();
            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>();

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, null)).ReturnsAsync(orgs);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, orgs, null)).ReturnsAsync(teamProjects);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, teamProjects, null)).ReturnsAsync(repos);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _scriptOutput.Should().BeNullOrWhiteSpace();
        }

        [Fact]
        public async Task SequentialScript_StartsWith_Shebang()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoOrg = ADO_ORG,
                GithubOrg = GITHUB_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _scriptOutput.Should().StartWith("#!/usr/bin/env pwsh");
        }

        [Fact]
        public async Task SequentialScript_Single_Repo_No_Options()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);
            var expected = $"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}";

            // Assert
            _scriptOutput.Should().Be(expected);
        }

        [Fact]
        public async Task SequentialScript_Single_Repo_All_Options()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);
            _mockAdoInspector.Setup(m => m.GetPipelines(_mockAdoApi.Object, ADO_REPOS)).ReturnsAsync(EMPTY_PIPELINES);

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);

            // Assert
            _scriptOutput.Should().Be(expected);
        }

        [Fact]
        public async Task SequentialScript_Skips_Team_Project_With_No_Repos()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(EMPTY_REPOS);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);

            // Assert
            _scriptOutput.Should().BeEmpty();
        }

        [Fact]
        public async Task SequentialScript_Single_Repo_Two_Pipelines_All_Options()
        {
            // Arrange
            ADO_PIPELINES[ADO_ORG][ADO_TEAM_PROJECT][FOO_REPO] = new List<string>() { ADO_PIPELINES[ADO_ORG][ADO_TEAM_PROJECT][FOO_REPO].First(), BAR_PIPELINE };

            _mockAdoApi.Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, new[] { ADO_TEAM_PROJECT })).ReturnsAsync(APP_ID);

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);
            _mockAdoInspector.Setup(m => m.GetPipelines(_mockAdoApi.Object, ADO_REPOS)).ReturnsAsync(ADO_PIPELINES);

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{BAR_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);

            // Assert
            _scriptOutput.Should().Be(expected);
        }

        [Fact]
        public async Task SequentialScript_Single_Repo_Two_Pipelines_No_Service_Connection_All_Options()
        {
            // Arrange
            ADO_PIPELINES[ADO_ORG][ADO_TEAM_PROJECT][FOO_REPO] = new List<string>() { ADO_PIPELINES[ADO_ORG][ADO_TEAM_PROJECT][FOO_REPO].First(), BAR_PIPELINE };

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);
            _mockAdoInspector.Setup(m => m.GetPipelines(_mockAdoApi.Object, ADO_REPOS)).ReturnsAsync(ADO_PIPELINES);

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);

            // Assert
            _scriptOutput.Should().Be(expected);
        }

        [Fact]
        public async Task SequentialScript_Create_Teams_Option_Should_Generate_Create_Team_And_Add_Teams_To_Repos_Scripts()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");
            expected.AppendLine($"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.Append($"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                CreateTeams = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Link_Idp_Groups_Option_Should_Generate_Create_Teams_Scripts_With_Idp_Groups()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");
            expected.AppendLine($"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.Append($"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                LinkIdpGroups = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Lock_Ado_Repo_Option_Should_Generate_Lock_Ado_Repo_Script()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.Append($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                LockAdoRepos = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Disable_Ado_Repo_Option_Should_Generate_Disable_Ado_Repo_Script()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");
            expected.Append($"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                DisableAdoRepos = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);

            // Assert
            _scriptOutput.Should().Contain(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Integrate_Boards_Option_Should_Generate_Auto_Link_And_Boards_Integration_Scripts()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");
            expected.AppendLine($"Exec {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}");
            expected.Append($"Exec {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                IntegrateBoards = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);

            // Assert
            _scriptOutput.Should().Contain(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Rewire_Pipelines_Option_Should_Generate_Share_Service_Connection_And_Rewire_Pipeline_Scripts()
        {
            // Arrange
            _mockAdoApi
                .Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, new[] { ADO_TEAM_PROJECT }))
                .ReturnsAsync(APP_ID);

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);
            _mockAdoInspector.Setup(m => m.GetPipelines(_mockAdoApi.Object, ADO_REPOS)).ReturnsAsync(ADO_PIPELINES);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");
            expected.Append($"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                RewirePipelines = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput);

            // Assert
            _scriptOutput.Should().Contain(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_No_Data()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, null)).ReturnsAsync(EMPTY_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, EMPTY_ORGS, null)).ReturnsAsync(EMPTY_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, EMPTY_TEAM_PROJECTS, null)).ReturnsAsync(EMPTY_REPOS);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _scriptOutput.Should().BeEmpty();
        }

        [Fact]
        public async Task ParallelScript_StartsWith_Shebang()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoOrg = ADO_ORG,
                GithubOrg = GITHUB_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _scriptOutput.Should().StartWith("#!/usr/bin/env pwsh");
        }

        [Fact]
        public async Task ParallelScript_Single_Repo_No_Options()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1.1 ===========");
            expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
            expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Queueing migration for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {ADO_ORG}/{ADO_TEAM_PROJECT} ===");
            expected.AppendLine();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {FOO_REPO}. Will then complete the below post migration steps. ===");
            expected.AppendLine("$CanExecuteBatch = $true");
            expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
            expected.AppendLine($"    ./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
            expected.AppendLine("}");
            expected.AppendLine("if ($CanExecuteBatch) {");
            expected.AppendLine("    $Succeeded++");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine("Write-Host =============== Summary ===============");
            expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
            expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
            expected.AppendLine();
            expected.AppendLine();

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task PatallelScript_Skips_Team_Project_With_No_Repos()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(EMPTY_REPOS);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput, 35, 6);

            // Assert
            _scriptOutput.Should().BeEmpty();
        }

        [Fact]
        public async Task ParallelScript_Two_Repos_Two_Pipelines_All_Options()
        {
            // Arrange
            ADO_REPOS[ADO_ORG][ADO_TEAM_PROJECT] = new List<string>() { FOO_REPO, BAR_REPO };
            ADO_PIPELINES[ADO_ORG][ADO_TEAM_PROJECT].Add(BAR_REPO, new List<string>() { BAR_PIPELINE });

            _mockAdoApi.Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, new[] { ADO_TEAM_PROJECT })).ReturnsAsync(APP_ID);

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);
            _mockAdoInspector.Setup(m => m.GetPipelines(_mockAdoApi.Object, ADO_REPOS)).ReturnsAsync(ADO_PIPELINES);

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1.1 ===========");
            expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
            expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Queueing migration for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {ADO_ORG}/{ADO_TEAM_PROJECT} ===");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{BAR_REPO}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{BAR_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{BAR_REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {FOO_REPO}. Will then complete the below post migration steps. ===");
            expected.AppendLine("$CanExecuteBatch = $true");
            expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
            expected.AppendLine($"    ./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
            expected.AppendLine("}");
            expected.AppendLine("if ($CanExecuteBatch) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {BAR_REPO}. Will then complete the below post migration steps. ===");
            expected.AppendLine("$CanExecuteBatch = $true");
            expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{BAR_REPO}\"]) {{");
            expected.AppendLine($"    ./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{BAR_REPO}\"]");
            expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
            expected.AppendLine("}");
            expected.AppendLine("if ($CanExecuteBatch) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{BAR_REPO}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" }}");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{BAR_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine("Write-Host =============== Summary ===============");
            expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
            expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
            expected.AppendLine();
            expected.AppendLine();

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await _command.Invoke(args);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Single_Repo_No_Service_Connection_All_Options()
        {
            // Arrange
            ADO_PIPELINES[ADO_ORG][ADO_TEAM_PROJECT][FOO_REPO] = new List<string>() { ADO_PIPELINES[ADO_ORG][ADO_TEAM_PROJECT][FOO_REPO].First(), BAR_PIPELINE };

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);
            _mockAdoInspector.Setup(m => m.GetPipelines(_mockAdoApi.Object, ADO_REPOS)).ReturnsAsync(ADO_PIPELINES);

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1.1 ===========");
            expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
            expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Queueing migration for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {ADO_ORG}/{ADO_TEAM_PROJECT} ===");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {FOO_REPO}. Will then complete the below post migration steps. ===");
            expected.AppendLine("$CanExecuteBatch = $true");
            expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
            expected.AppendLine($"    ./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
            expected.AppendLine("}");
            expected.AppendLine("if ($CanExecuteBatch) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine("Write-Host =============== Summary ===============");
            expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
            expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
            expected.AppendLine();
            expected.AppendLine();

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await _command.Invoke(args);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Create_Teams_Option_Should_Generate_Create_Teams_And_Add_Teams_To_Repos_Scripts()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine("$CanExecuteBatch = $true");
            expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
            expected.AppendLine($"    ./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
            expected.AppendLine("}");
            expected.AppendLine("if ($CanExecuteBatch) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                CreateTeams = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput, 35, 6);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Link_Idp_Groups_Option_Should_Generate_Create_Teams_Scripts_With_Idp_Groups()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine("$CanExecuteBatch = $true");
            expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
            expected.AppendLine($"    ./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
            expected.AppendLine("}");
            expected.AppendLine("if ($CanExecuteBatch) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                LinkIdpGroups = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput, 35, 6);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Lock_Ado_Repo_Option_Should_Generate_Lock_Ado_Repo_Script()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine("$CanExecuteBatch = $true");
            expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
            expected.AppendLine($"    ./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
            expected.AppendLine("}");
            expected.AppendLine("if ($CanExecuteBatch) {");
            expected.AppendLine("    $Succeeded++");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                LockAdoRepos = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput, 35, 6);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Disable_Ado_Repo_Option_Should_Generate_Disable_Ado_Repo_Script()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine("$CanExecuteBatch = $true");
            expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
            expected.AppendLine($"    ./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
            expected.AppendLine("}");
            expected.AppendLine("if ($CanExecuteBatch) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                DisableAdoRepos = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput, 35, 6);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Integrate_Boards_Option_Should_Generate_Auto_Link_And_Boards_Integration_Scripts()
        {
            // Arrange
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);

            var expected = new StringBuilder();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine("$CanExecuteBatch = $true");
            expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
            expected.AppendLine($"    ./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
            expected.AppendLine("}");
            expected.AppendLine("if ($CanExecuteBatch) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                IntegrateBoards = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput, 35, 6);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Rewire_Pipelines_Option_Should_Generate_Share_Service_Connection_And_Rewire_Pipeline_Scripts()
        {
            // Arrange
            _mockAdoApi.Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, new[] { ADO_TEAM_PROJECT })).ReturnsAsync(APP_ID);

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);
            _mockAdoInspector.Setup(m => m.GetPipelines(_mockAdoApi.Object, ADO_REPOS)).ReturnsAsync(ADO_PIPELINES);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine("$CanExecuteBatch = $true");
            expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
            expected.AppendLine($"    ./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
            expected.AppendLine("}");
            expected.AppendLine("if ($CanExecuteBatch) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                RewirePipelines = true
            };
            await _command.Invoke(args);

            _scriptOutput = TrimNonExecutableLines(_scriptOutput, 35, 6);

            // Assert
            _scriptOutput.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            // Arrange
            const string adoPat = "ado-pat";

            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(EMPTY_REPOS);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                AdoPat = adoPat,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }

        private string TrimNonExecutableLines(string script, int skipFirst = 9, int skipLast = 0)
        {
            var lines = script.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

            lines = lines
                .Where(x => x.HasValue())
                .Where(x => !x.Trim().StartsWith("#"))
                .Skip(skipFirst)
                .SkipLast(skipLast);

            return string.Join(Environment.NewLine, lines);
        }
    }
}
