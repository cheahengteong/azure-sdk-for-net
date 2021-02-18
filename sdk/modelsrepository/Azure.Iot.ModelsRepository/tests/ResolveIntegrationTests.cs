// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Iot.ModelsRepository.Tests
{
    public class ResolveIntegrationTests : ModelRepoRecordedTestBase
    {
        public ResolveIntegrationTests(bool isAsync) : base(isAsync)
        {
        }

        [TestCase(ModelRepoTestBase.ClientType.Local)]
        [TestCase(ModelRepoTestBase.ClientType.Remote)]
        public void ResolveWithWrongCasingThrowsException(ModelRepoTestBase.ClientType clientType)
        {
            const string dtmi = "dtmi:com:example:thermostat;1";

            ResolverClient client = GetClient(clientType);
            string expectedExMsg =
                string.Format(ServiceStrings.GenericResolverError, "dtmi:com:example:thermostat;1") +
                " " +
                string.Format(ServiceStrings.IncorrectDtmiCasing, "dtmi:com:example:thermostat;1", "dtmi:com:example:Thermostat;1");

            ResolverException re = Assert.ThrowsAsync<ResolverException>(async () => await client.ResolveAsync(dtmi));
            Assert.AreEqual(re.Message, expectedExMsg);
        }

        [TestCase("dtmi:com:example:Thermostat:1")]
        [TestCase("dtmi:com:example::Thermostat;1")]
        [TestCase("com:example:Thermostat;1")]
        public void ResolveInvalidDtmiFormatThrowsException(string dtmi)
        {
            ResolverClient client = GetClient(ModelRepoTestBase.ClientType.Local);
            string expectedExMsg = $"{string.Format(ServiceStrings.GenericResolverError, dtmi)} {string.Format(ServiceStrings.InvalidDtmiFormat, dtmi)}";
            ResolverException re = Assert.ThrowsAsync<ResolverException>(async () => await client.ResolveAsync(dtmi));
            Assert.AreEqual(re.Message, expectedExMsg);
        }

        [TestCase(ModelRepoTestBase.ClientType.Local)]
        [TestCase(ModelRepoTestBase.ClientType.Remote)]
        public void ResolveNoneExistentDtmiFileThrowsException(ModelRepoTestBase.ClientType clientType)
        {
            const string dtmi = "dtmi:com:example:thermojax;999";

            ResolverClient client = GetClient(clientType);
            ResolverException re = Assert.ThrowsAsync<ResolverException>(async () => await client.ResolveAsync(dtmi));
            Assert.True(re.Message.StartsWith($"Unable to resolve \"{dtmi}\""));
        }

        public void ResolveInvalidDtmiDepsThrowsException()
        {
            const string dtmi = "dtmi:com:example:invalidmodel;1";
            const string invalidDep = "dtmi:azure:fakeDeviceManagement:FakeDeviceInformation;2";

            ResolverClient client = GetClient(ModelRepoTestBase.ClientType.Local);
            ResolverException resolverException = Assert.ThrowsAsync<ResolverException>(async () => await client.ResolveAsync(dtmi));
            Assert.True(resolverException.Message.StartsWith($"Unable to resolve \"{invalidDep}\""));
        }

        [TestCase(ModelRepoTestBase.ClientType.Local)]
        [TestCase(ModelRepoTestBase.ClientType.Remote)]
        public async Task ResolveSingleModelNoDeps(ModelRepoTestBase.ClientType clientType)
        {
            const string dtmi = "dtmi:com:example:Thermostat;1";

            ResolverClient client = GetClient(clientType);
            IDictionary<string, string> result = await client.ResolveAsync(dtmi);
            Assert.True(result.Keys.Count == 1);
            Assert.True(result.ContainsKey(dtmi));
            Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[dtmi]) == dtmi);
        }

        [TestCase(ModelRepoTestBase.ClientType.Local)]
        [TestCase(ModelRepoTestBase.ClientType.Remote)]
        public async Task ResolveMultipleModelsNoDeps(ModelRepoTestBase.ClientType clientType)
        {
            const string dtmi1 = "dtmi:com:example:Thermostat;1";
            const string dtmi2 = "dtmi:azure:DeviceManagement:DeviceInformation;1";

            ResolverClient client = GetClient(clientType);
            IDictionary<string, string> result = await client.ResolveAsync(new string[] { dtmi1, dtmi2 });
            Assert.True(result.Keys.Count == 2);
            Assert.True(result.ContainsKey(dtmi1));
            Assert.True(result.ContainsKey(dtmi2));
            Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[dtmi1]) == dtmi1);
            Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[dtmi2]) == dtmi2);
        }

        [TestCase(ModelRepoTestBase.ClientType.Local)]
        [TestCase(ModelRepoTestBase.ClientType.Remote)]
        public async Task ResolveSingleModelWithDeps(ModelRepoTestBase.ClientType clientType)
        {
            const string dtmi = "dtmi:com:example:TemperatureController;1";
            const string expectedDeps = "dtmi:com:example:Thermostat;1,dtmi:azure:DeviceManagement:DeviceInformation;1";

            ResolverClient client = GetClient(clientType);
            IDictionary<string, string> result = await client.ResolveAsync(dtmi);
            var expectedDtmis = $"{dtmi},{expectedDeps}".Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            Assert.True(result.Keys.Count == expectedDtmis.Length);
            foreach (var id in expectedDtmis)
            {
                Assert.True(result.ContainsKey(id));
                Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[id]) == id);
            }

            // TODO: Evaluate using Azure.Core.TestFramework in future iteration.

            /*
             // Verifying log entries for a Process(...) run
            _logger.ValidateLog($"{ServiceStringss.ClientInitWithFetcher(localClient.RepositoryUri.Scheme)}", LogLevel.Trace, Times.Once());

            _logger.ValidateLog($"{ServiceStringss.ProcessingDtmi("dtmi:com:example:TemperatureController;1")}", LogLevel.Trace, Times.Once());
            _logger.ValidateLog($"{ServiceStringss.FetchingContent(DtmiConventions.DtmiToQualifiedPath(expectedDtmis[0], localClient.RepositoryUri.AbsolutePath))}", LogLevel.Trace, Times.Once());

            _logger.ValidateLog($"{ServiceStringss.DiscoveredDependencies(new List<string>() { "dtmi:com:example:Thermostat;1", "dtmi:azure:DeviceManagement:DeviceInformation;1" })}", LogLevel.Trace, Times.Once());

            _logger.ValidateLog($"{ServiceStringss.ProcessingDtmi("dtmi:com:example:Thermostat;1")}", LogLevel.Trace, Times.Once());
            _logger.ValidateLog($"{ServiceStringss.FetchingContent(DtmiConventions.DtmiToQualifiedPath(expectedDtmis[1], localClient.RepositoryUri.AbsolutePath))}", LogLevel.Trace, Times.Once());

            _logger.ValidateLog($"{ServiceStringss.ProcessingDtmi("dtmi:azure:DeviceManagement:DeviceInformation;1")}", LogLevel.Trace, Times.Once());
            _logger.ValidateLog($"{ServiceStringss.FetchingContent(DtmiConventions.DtmiToQualifiedPath(expectedDtmis[2], localClient.RepositoryUri.AbsolutePath))}", LogLevel.Trace, Times.Once());
            */
        }

        public async Task ResolveMultipleModelsWithDeps()
        {
            const string dtmi1 = "dtmi:com:example:Phone;2";
            const string dtmi2 = "dtmi:com:example:TemperatureController;1";
            const string expectedDeps = "dtmi:com:example:Thermostat;1," +
                  "dtmi:azure:DeviceManagement:DeviceInformation;1," +
                  "dtmi:azure:DeviceManagement:DeviceInformation;2," +
                  "dtmi:com:example:Camera;3";

            ResolverClient client = GetClient(ModelRepoTestBase.ClientType.Local);
            IDictionary<string, string> result = await client.ResolveAsync(new[] { dtmi1, dtmi2 });
            var expectedDtmis = $"{dtmi1},{dtmi2},{expectedDeps}".Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            Assert.True(result.Keys.Count == expectedDtmis.Length);
            foreach (var id in expectedDtmis)
            {
                Assert.True(result.ContainsKey(id));
                Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[id]) == id);
            }
        }

        public async Task ResolveMultipleModelsWithDepsFromExtends()
        {
            const string dtmi1 = "dtmi:com:example:TemperatureController;1";
            const string dtmi2 = "dtmi:com:example:ConferenceRoom;1";
            const string expectedDeps = "dtmi:com:example:Thermostat;1,dtmi:azure:DeviceManagement:DeviceInformation;1,dtmi:com:example:Room;1";
            ResolverClient client = GetClient(ModelRepoTestBase.ClientType.Local);
            IDictionary<string, string> result = await client.ResolveAsync(new[] { dtmi1, dtmi2 });
            var expectedDtmis = $"{dtmi1},{dtmi2},{expectedDeps}".Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            Assert.True(result.Keys.Count == expectedDtmis.Length);
            foreach (var id in expectedDtmis)
            {
                Assert.True(result.ContainsKey(id));
                Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[id]) == id);
            }
        }

        public async Task ResolveMultipleModelsWithDepsFromExtendsVariant()
        {
            const string dtmi1 = "dtmi:com:example:TemperatureController;1";
            const string dtmi2 = "dtmi:com:example:ColdStorage;1";
            const string expectedDeps = "dtmi:com:example:Thermostat;1," +
                  "dtmi:azure:DeviceManagement:DeviceInformation;1," +
                  "dtmi:com:example:Room;1," +
                  "dtmi:com:example:Freezer;1";

            ResolverClient client = GetClient(ModelRepoTestBase.ClientType.Local);
            IDictionary<string, string> result = await client.ResolveAsync(new[] { dtmi1, dtmi2 });
            var expectedDtmis = $"{dtmi1},{dtmi2},{expectedDeps}".Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            Assert.True(result.Keys.Count == expectedDtmis.Length);
            foreach (var id in expectedDtmis)
            {
                Assert.True(result.ContainsKey(id));
                Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[id]) == id);
            }
        }

        public async Task ResolveSingleModelWithDepsFromExtendsInline()
        {
            const string dtmi = "dtmi:com:example:base;1";
            ResolverClient client = GetClient(ModelRepoTestBase.ClientType.Local);
            IDictionary<string, string> result = await client.ResolveAsync(dtmi);

            Assert.True(result.Keys.Count == 1);
            Assert.True(result.ContainsKey(dtmi));
            Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[dtmi]) == dtmi);
        }

        public async Task ResolveSingleModelWithDepsFromExtendsInlineVariant()
        {
            const string dtmi = "dtmi:com:example:base;2";
            const string expected = "dtmi:com:example:Freezer;1," +
                  "dtmi:com:example:Thermostat;1";

            ResolverClient client = GetClient(ModelRepoTestBase.ClientType.Local);
            IDictionary<string, string> result = await client.ResolveAsync(dtmi);
            var expectedDtmis = $"{dtmi},{expected}".Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            Assert.True(result.Keys.Count == expectedDtmis.Length);
            foreach (var id in expectedDtmis)
            {
                Assert.True(result.ContainsKey(id));
                Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[id]) == id);
            }
        }

        public async Task ResolveEnsuresNoDupes()
        {
            const string dtmiDupe1 = "dtmi:azure:DeviceManagement:DeviceInformation;1";
            const string dtmiDupe2 = "dtmi:azure:DeviceManagement:DeviceInformation;1";

            ResolverClient client = GetClient(ModelRepoTestBase.ClientType.Local);
            IDictionary<string, string> result = await client.ResolveAsync(new[] { dtmiDupe1, dtmiDupe2 });
            Assert.True(result.Keys.Count == 1);
            Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[dtmiDupe1]) == dtmiDupe1);
        }

        [TestCase(ModelRepoTestBase.ClientType.Local)]
        [TestCase(ModelRepoTestBase.ClientType.Remote)]
        public async Task ResolveSingleModelWithDepsDisableDependencyResolution(ModelRepoTestBase.ClientType clientType)
        {
            const string dtmi = "dtmi:com:example:Thermostat;1";

            ResolverClientOptions options = new ResolverClientOptions(resolutionOption: DependencyResolutionOption.Disabled);
            ResolverClient client = GetClient(clientType, options);

            IDictionary<string, string> result = await client.ResolveAsync(dtmi);

            Assert.True(result.Keys.Count == 1);
            Assert.True(result.ContainsKey(dtmi));
            Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[dtmi]) == dtmi);
        }

        [TestCase(ModelRepoTestBase.ClientType.Local)]
        [TestCase(ModelRepoTestBase.ClientType.Remote)]
        public async Task ResolveSingleModelTryFromExpanded(ModelRepoTestBase.ClientType clientType)
        {
            const string dtmi = "dtmi:com:example:TemperatureController;1";
            const string expectedDeps = "dtmi:com:example:Thermostat;1,dtmi:azure:DeviceManagement:DeviceInformation;1";

            var expectedDtmis = $"{dtmi},{expectedDeps}".Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            ResolverClientOptions options = new ResolverClientOptions(resolutionOption: DependencyResolutionOption.TryFromExpanded);
            ResolverClient client = GetClient(clientType, options);

            IDictionary<string, string> result = await client.ResolveAsync(dtmi);

            Assert.True(result.Keys.Count == expectedDtmis.Length);
            foreach (var id in expectedDtmis)
            {
                Assert.True(result.ContainsKey(id));
                Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[id]) == id);
            }

            // TODO: Evaluate using Azure.Core.TestFramework in future iteration.

            /*
            string expectedPath = DtmiConventions.DtmiToQualifiedPath(
                dtmi,
                repoType == "local" ? client.RepositoryUri.AbsolutePath : client.RepositoryUri.AbsoluteUri,
                fromExpanded: true);
            _logger.ValidateLog(ServiceStringss.FetchingContent(expectedPath), LogLevel.Trace, Times.Once());
            */
        }

        public async Task ResolveMultipleModelsTryFromExpandedPartial()
        {
            const string dtmisExpanded = "dtmi:com:example:TemperatureController;1," +  // Expanded available.
                  "dtmi:com:example:Thermostat;1," +
                  "dtmi:azure:DeviceManagement:DeviceInformation;1";

            const string dtmisNonExpanded = "dtmi:com:example:ColdStorage;1," + // Model uses extends[], No Expanded available.
                  "dtmi:com:example:Room;1," +
                  "dtmi:com:example:Freezer;1";

            string[] expandedDtmis = dtmisExpanded.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            string[] nonExpandedDtmis = dtmisNonExpanded.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            string[] totalDtmis = expandedDtmis.Concat(nonExpandedDtmis).ToArray();

            ResolverClientOptions options = new ResolverClientOptions(resolutionOption: DependencyResolutionOption.TryFromExpanded);
            ResolverClient client = GetClient(ModelRepoTestBase.ClientType.Local, options);

            // Multi-resolve dtmi:com:example:TemperatureController;1 + dtmi:com:example:ColdStorage;1
            IDictionary<string, string> result = await client.ResolveAsync(new[] { expandedDtmis[0], nonExpandedDtmis[0] });

            Assert.True(result.Keys.Count == totalDtmis.Length);
            foreach (string id in totalDtmis)
            {
                Assert.True(result.ContainsKey(id));
                Assert.True(ModelRepoTestBase.ParseRootDtmiFromJson(result[id]) == id);
            }

            // TODO: Evaluate using Azure.Core.TestFramework in future iteration.

            /*
            string expandedModelPath = DtmiConventions.DtmiToQualifiedPath(expandedDtmis[0], localClient.RepositoryUri.AbsolutePath, fromExpanded: true);
            _logger.ValidateLog(ServiceStrings.FetchingContent(expandedModelPath), LogLevel.Trace, Times.Once());

            foreach (string dtmi in nonExpandedDtmis)
            {
                string expectedPath = DtmiConventions.DtmiToQualifiedPath(dtmi, localClient.RepositoryUri.AbsolutePath, fromExpanded: true);
                _logger.ValidateLog(ServiceStrings.FetchingContent(expectedPath), LogLevel.Trace, Times.Once());
                _logger.ValidateLog(ServiceStrings.ErrorAccessLocalRepositoryModel(expectedPath), LogLevel.Warning, Times.Once());
            }
            */
        }
    }
}
