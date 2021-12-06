﻿using System.IO;
using System.Linq;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class AdoApiTests
    {
        [Fact]
        public async void Get_User_Id_Test()
        {
            var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
            var userId = "foo";
            var userJson = "{ coreAttributes: { PublicAlias: { value: \"" + userId + "\" }}}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetUserId();

            Assert.Equal(userId, result);
        }

        [Fact]
        public async void Get_User_Id_Invalid_Response()
        {
            var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
            var userId = "foo";
            var userJson = "{ invalid: { PublicAlias: { value: \"" + userId + "\" }}}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson);

            using var sut = new AdoApi(mockClient.Object);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await sut.GetUserId());
        }

        [Fact]
        public async void GetOrganizations()
        {
            var userId = "foo";
            var endpoint = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId}?api-version=5.0-preview.1";
            var accountsJson = "[{accountId: 'blah', AccountName: 'foo'}, {AccountName: 'foo2'}]";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(accountsJson);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetOrganizations(userId);

            Assert.Equal(2, result.Count());
            Assert.Contains(result, x => x == "foo");
            Assert.Contains(result, x => x == "foo2");
        }

        [Fact]
        public async void GetOrganizationId()
        {
            var userId = "foo";
            var adoOrg = "foo-org";
            var orgId = "blah";
            var endpoint = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId}&api-version=5.0-preview.1";
            var accountsJson = "[{accountId: '" + orgId + "', accountName: '" + adoOrg + "'}, {accountName: 'foo2', accountId: 'asdf'}]";
            var response = JArray.Parse(accountsJson);

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetOrganizationId(userId, adoOrg);

            Assert.Equal("blah", result);
        }

        [Fact]
        public async void GetTeamProjectsTwoProjects()
        {
            var adoOrg = "foo-org";
            var teamProject1 = "foo-tp";
            var teamProject2 = "foo-tp2";
            var endpoint = $"https://dev.azure.com/{adoOrg}/_apis/projects?api-version=6.1-preview";
            var json = "[{somethingElse: false, name: '" + teamProject1 + "'}, {id: 'sfasfasdf', name: '" + teamProject2 + "'}]";
            var response = JArray.Parse(json);

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetTeamProjects(adoOrg);

            Assert.Equal(2, result.Count());
            Assert.Contains(result, x => x == teamProject1);
            Assert.Contains(result, x => x == teamProject2);
        }

        [Fact]
        public async void GetReposThreeReposOneDisabled()
        {
            var adoOrg = "foo-org";
            var teamProject = "foo-tp";
            var repo1 = "foo-repo";
            var repo2 = "foo-repo2";
            var endpoint = $"https://dev.azure.com/{adoOrg}/{teamProject}/_apis/git/repositories?api-version=6.1-preview.1";
            var json = "[{isDisabled: 'true', name: 'testing'}, {isDisabled: false, name: '" + repo1 + "'}, {isDisabled: 'FALSE', name: '" + repo2 + "'}]";
            var response = JArray.Parse(json);

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetRepos(adoOrg, teamProject);

            Assert.Equal(2, result.Count());
            Assert.Contains(result, x => x == repo1);
            Assert.Contains(result, x => x == repo2);
        }
    }
}