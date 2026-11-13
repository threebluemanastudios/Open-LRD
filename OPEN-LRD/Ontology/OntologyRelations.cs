using OPEN_LRD.NLP;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Ontology
{
    internal sealed record OntologyRelation(
        int Id,
        string Name,
        string InverseName,
        string LinkClassName);

    // Fixed structural relations from the reserved 1-999 ID range. IDs are
    // hardcoded so they stay stable across runs and across persisted logs.
    internal static class OntologyRelations
    {
        public const int IsA = 1;
        public const int InstanceOf = 2;
        public const int PartOf = 3;
        public const int HasProperty = 4;
        public const int Causes = 5;
        public const int ParticipatedIn = 6;
        public const int HasTime = 7;
        public const int HasLocation = 8;
        public const int IsNamed = 9;
        public const int HasSense = 10;
        public const int Synonym = 11;
        public const int RelatedTo = 12;
        public const int HasPos = 13;
        public const int HasField = 14;
        public const int MemberOf = 15;
        public const int ComposedOf = 16;
        public const int Produces = 17;
        public const int Uses = 18;
        public const int HasReality = 19;

        public const int Antonym = RelationRegistry.AntonymLinkTypeId;

        public static readonly IReadOnlyList<OntologyRelation> All = new OntologyRelation[]
        {
            new OntologyRelation(IsA, "is_a", "has_subtype", nameof(OntologyTypes.ontology)),
            new OntologyRelation(InstanceOf, "instance_of", "has_instance", nameof(OntologyTypes.ontology)),
            new OntologyRelation(PartOf, "part_of", "has_part", nameof(OntologyTypes.ontology)),
            new OntologyRelation(HasProperty, "has_property", "property_of", nameof(OntologyTypes.properties)),
            new OntologyRelation(Causes, "causes", "caused_by", nameof(OntologyTypes.ontology)),
            new OntologyRelation(ParticipatedIn, "participated_in", "has_participant", nameof(OntologyTypes.ontology)),
            new OntologyRelation(HasTime, "has_time", "time_of", nameof(OntologyTypes.ontology)),
            new OntologyRelation(HasLocation, "has_location", "location_of", nameof(OntologyTypes.ontology)),
            new OntologyRelation(IsNamed, "is_named", "names", nameof(OntologyTypes.lexicon)),
            new OntologyRelation(HasSense, "has_sense", "sense_of", nameof(OntologyTypes.lexicon)),
            new OntologyRelation(Synonym, "synonym", "synonym", nameof(OntologyTypes.lexicon)),
            new OntologyRelation(RelatedTo, "related_to", "related_to", nameof(OntologyTypes.lexicon)),
            new OntologyRelation(HasPos, "has_pos", "pos_of", nameof(OntologyTypes.lexicon)),
            new OntologyRelation(HasField, "has_field", "field_of", nameof(OntologyTypes.lexicon)),
            new OntologyRelation(MemberOf, "member_of", "has_member", nameof(OntologyTypes.ontology)),
            new OntologyRelation(ComposedOf, "composed_of", "component_of", nameof(OntologyTypes.ontology)),
            new OntologyRelation(Produces, "produces", "produced_by", nameof(OntologyTypes.ontology)),
            new OntologyRelation(Uses, "uses", "used_by", nameof(OntologyTypes.ontology)),
            new OntologyRelation(HasReality, "has_reality", "reality_of", nameof(OntologyTypes.ontology))
        };

        private static bool registered;

        public static void EnsureRegistered()
        {
            if (registered)
                return;

            OntologyTypes.EnsureRegistered();

            foreach (OntologyRelation relation in All)
            {
                RelationRegistry.RegisterOntologyRelation(
                    relation.Id,
                    relation.Name,
                    relation.InverseName,
                    out _);
            }

            registered = true;
        }

        public static LinkClass LinkClassFor(string relationName)
        {
            foreach (OntologyRelation relation in All)
            {
                if (string.Equals(relation.Name, relationName, StringComparison.OrdinalIgnoreCase))
                    return ResolveLinkClass(relation.LinkClassName);
            }

            return OntologyTypes.ontology;
        }

        private static LinkClass ResolveLinkClass(string name)
        {
            return name switch
            {
                nameof(OntologyTypes.lexicon) => OntologyTypes.lexicon,
                nameof(OntologyTypes.taxonomy) => OntologyTypes.taxonomy,
                nameof(OntologyTypes.properties) => OntologyTypes.properties,
                _ => OntologyTypes.ontology
            };
        }
    }
}
