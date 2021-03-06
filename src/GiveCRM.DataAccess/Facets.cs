﻿using System;
using System.Linq;
using System.Collections.Generic;

using GiveCRM.Models;
using Simple.Data;

namespace GiveCRM.DataAccess
{
    public interface IFacets
    {
        Facet Get(int id);
        IEnumerable<Facet> All();
        Facet Insert(Facet facet);
        void Update(Facet facet);
        IEnumerable<Facet> AllFreeText();
    }

    public class Facets : IFacets
    {
        private readonly dynamic db = Database.OpenNamedConnection("GiveCRM");

        public Facet Get(int id)
        {
            var record = db.Facets.FindById(id);
            Facet facet = record;
            facet.Values = record.FacetValues.ToList<FacetValue>();
            return facet;
        }

        public IEnumerable<Facet> All()
        {
            var query = db.Facets.All()
                .Select(db.Facets.Id, db.Facets.Type, db.Facets.Name,
                db.Facets.FacetValues.Id.As("FacetValueId"), db.Facets.FacetValues.FacetId, db.Facets.FacetValues.Value)
                .OrderBy(db.Facets.Id);

            Facet facet = null;

            foreach (var row in query)
            {
                if (facet == null)
                {
                    facet = row;
                }
                if (row.FacetId == facet.Id)
                {
                    if (facet.Values == null)
                    {
                        facet.Values = new List<FacetValue>();
                    }
                    facet.Values.Add(new FacetValue { FacetId = row.Id, Id = row.FacetValueId, Value = row.Value });
                }
                else
                {
                    yield return facet;
                    facet = null;
                }
            }

            if (facet != null)
            {
                yield return facet;
            }
        }

        public Facet Insert(Facet facet)
        {
            if (facet.Values != null && facet.Values.Count > 0)
            {
                return InsertWithValues(facet);
            }
            var record = db.Facets.Insert(facet);
            return record;
        }

        public void Update(Facet facet)
        {
            if (facet.Values != null && facet.Values.Count > 0)
            {
                UpdateWithValues(facet);
            }
            else
            {
                db.Facets.UpdateById(facet);
            }
        }

        private void UpdateWithValues(Facet facet)
        {
            using (var transaction = db.BeginTransaction())
            {
                try
                {
                    transaction.Facets.UpdateById(facet);
                    foreach (var facetValue in facet.Values)
                    {
                        if (facetValue.Id == 0)
                        {
                            facetValue.FacetId = facet.Id;
                            transaction.FacetValues.Insert(facetValue);
                        }
                        else
                        {
                            transaction.FacetValues.UpdateById(facetValue);
                        }
                    }
                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private Facet InsertWithValues(Facet facet)
        {
            using (var transaction = db.BeginTransaction())
            {
                try
                {
                    Facet inserted = transaction.Facets.Insert(facet);
                    foreach (var facetValue in facet.Values)
                    {
                        facetValue.FacetId = inserted.Id;
                    }
                    inserted.Values = transaction.FacetValues.Insert(facet.Values).ToList<FacetValue>();
                    transaction.Commit();
                    return inserted;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public IEnumerable<Facet> AllFreeText()
        {
            return db.Facets.FindAllByType(FacetType.FreeText.ToString()).Cast<Facet>();
        }
    }
}
