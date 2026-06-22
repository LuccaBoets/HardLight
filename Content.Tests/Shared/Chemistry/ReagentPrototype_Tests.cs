using System;
using System.IO;
using Content.Shared.Chemistry.Reagent;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Tests.Shared.Chemistry
{
    [TestFixture, TestOf(typeof(ReagentPrototype))]
    public sealed class ReagentPrototype_Tests : ContentUnitTest
    {
        [Test]
        public void DeserializeReagentPrototype()
        {
            using (TextReader stream = new StringReader(YamlReagentPrototype))
            {
                var yamlStream = new YamlStream();
                yamlStream.Load(stream);
                var document = yamlStream.Documents[0];
                var rootNode = (YamlSequenceNode)document.RootNode;
                var proto = (YamlMappingNode)rootNode[0];

                var defType = proto.GetNode("type").AsString();
                var serializationManager = IoCManager.Resolve<ISerializationManager>();
                serializationManager.Initialize();

                var newReagent = serializationManager.Read<ReagentPrototype>(new MappingDataNode(proto));

                Assert.That(defType, Is.EqualTo("reagent"));
                Assert.That(newReagent.ID, Is.EqualTo("H2"));
                Assert.That(newReagent.LocalizedName, Is.EqualTo("Hydrogen"));
                Assert.That(newReagent.LocalizedDescription, Is.EqualTo("A light, flammable gas."));
                Assert.That(newReagent.SubstanceColor, Is.EqualTo(Color.Teal));
            }
        }

        // Hardlight start
        [Test]
        public void DeserializeReagentPrototypeWithSpoilConditions()
        {
            const string yaml = @"- type: reagent
  id: TestSpoil
  name: Test
  desc: Testing reagent spoil conditions.
  physicalDesc: Test reagent.
  color: ""#ffffff""
  spoilConditions:
    bloodstreamPreserve: true
    spoilTime: 300
    spoilsInto: InertNanites
    allowedBloodTypes:
      - Blood
      - CopperBlood
";

            using TextReader stream = new StringReader(yaml);
            var yamlStream = new YamlStream();
            yamlStream.Load(stream);
            var document = yamlStream.Documents[0];
            var rootNode = (YamlSequenceNode)document.RootNode;
            var proto = (YamlMappingNode)rootNode[0];

            var serializationManager = IoCManager.Resolve<ISerializationManager>();

            var newReagent = serializationManager.Read<ReagentPrototype>(new MappingDataNode(proto));

            Assert.That(newReagent.ID, Is.EqualTo("TestSpoil"));
            Assert.That(newReagent.SpoilConditions, Is.Not.Null);
            Assert.That(newReagent.SpoilConditions!.SpoilsInto?.ToString(), Is.EqualTo("InertNanites"));
            Assert.That(newReagent.SpoilConditions.SpoilTime, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(newReagent.SpoilConditions.BloodstreamPreserve, Is.True);
            Assert.That(newReagent.SpoilConditions.AllowedBloodTypes, Is.EquivalentTo(new[] { "Blood", "CopperBlood" }));
        }
        // Hardlight end
        private const string YamlReagentPrototype = @"- type: reagent
  id: H2
  name: Hydrogen
  desc: A light, flammable gas.
  physicalDesc: A light, flammable gas.
  color: " + "\"#008080\"";
    }
}
