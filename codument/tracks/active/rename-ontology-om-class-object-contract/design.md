# Design

## 上下文

This track is the first implementation slice of mission `rename-om-class-object-model`. It owns the low-level Class/Object/Field/FieldValue rename in ontology core.

## 方案概览

1. Rename contracts at the edge first.
   - Replace public API methods and DTO/model names.
   - Replace sample and test call sites.
   - Keep no old method aliases.
2. Rename durable storage.
   - Update schema creation and schema definition metadata.
   - Update Cozo table names and columns in Type/Class and Entity/Object logic.
   - Update alias and hierarchy storage for Class/Field.
3. Update schema evolution and import/apply paths.
   - Rename JSON step field readers from Type/Attribute/Entity vocabulary to Class/Field/Object vocabulary.
   - Update schema diff fixtures and snapshot expectations.
4. Verify no legacy surface remains for this slice.
   - Build and run ontology tests.
   - Scan current source/tests/examples/docs for old Class/Object/Field/FieldValue predecessor names.

## 影响范围与修改点（Impact）

- API: `CozoOm`, extension packages, example server, tests.
- Models: OM contract models and any view DTOs that expose Type/Entity/Attribute/Property language.
- Storage: schema initialization, schema evolution, logic queries, aliases, mixins, object search, property history.

## 决策摘要

- The mission decision is authoritative: no compatibility.
- `Property` means durable `FieldValue` in this slice; computed values move to the later ComputedProp track.
- `value_type` becomes `value_kind` because it is a scalar/category discriminator, not an OM Class.

See `decisions.xnl` for the track-level process decision.

## 风险 / 权衡

- Risk: mechanical rename accidentally touches unrelated C# `Type` or generic "property" wording.
  - Mitigation: target old OM APIs/tables/columns first; use scan review instead of whole-word blanket replacement.
- Risk: schema evolution tests assume old tables for legacy migration coverage.
  - Mitigation: update or remove old compatibility fixtures according to the no-compatibility rule.
- Risk: computed property merge currently shares object view shape with durable properties.
  - Mitigation: keep only durable property rename here and leave ComputedProp shape to the second ontology track.

## 兼容性设计

This is a breaking track. It intentionally provides no old API aliases, adapters, or old table read/write fallback.

## 迁移计划

1. Update tests/fixtures to the target contract.
2. Update public API and models.
3. Update schema and logic queries.
4. Update examples and docs.
5. Run build/tests and scans.

## 待解决问题

- None blocking for this slice.
