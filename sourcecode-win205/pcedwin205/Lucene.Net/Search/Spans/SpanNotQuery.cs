/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using IndexReader = Lucene.Net.Index.IndexReader;
using Query = Lucene.Net.Search.Query;
using ToStringUtils = Lucene.Net.Util.ToStringUtils;

namespace Lucene.Net.Search.Spans
{
	
	/// <summary>Removes matches which overlap with another SpanQuery. </summary>
	[Serializable]
	public class SpanNotQuery : SpanQuery
	{
		private class AnonymousClassSpans : Spans
		{
			public AnonymousClassSpans(Lucene.Net.Index.IndexReader reader, SpanNotQuery enclosingInstance)
			{
				InitBlock(reader, enclosingInstance);
			}
			private void  InitBlock(Lucene.Net.Index.IndexReader reader, SpanNotQuery enclosingInstance)
			{
				this.reader = reader;
				this.enclosingInstance = enclosingInstance;
				includeSpans = Enclosing_Instance.include.GetSpans(reader);
				excludeSpans = Enclosing_Instance.exclude.GetSpans(reader);
                moreExclude = excludeSpans.Next();
			}
			private Lucene.Net.Index.IndexReader reader;
			private SpanNotQuery enclosingInstance;
			public SpanNotQuery Enclosing_Instance
			{
				get
				{
					return enclosingInstance;
				}
				
			}
			private Spans includeSpans;
			private bool moreInclude = true;
			
			private Spans excludeSpans;
			private bool moreExclude;
			
			public virtual bool Next()
			{
				if (moreInclude)
				// move to next include
					moreInclude = includeSpans.Next();
				
				while (moreInclude && moreExclude)
				{
					
					if (includeSpans.Doc() > excludeSpans.Doc())
					// skip exclude
						moreExclude = excludeSpans.SkipTo(includeSpans.Doc());
					
					while (moreExclude && includeSpans.Doc() == excludeSpans.Doc() && excludeSpans.End() <= includeSpans.Start())
					{
						moreExclude = excludeSpans.Next(); // increment exclude
					}
					
					if (!moreExclude || includeSpans.Doc() != excludeSpans.Doc() || includeSpans.End() <= excludeSpans.Start())
						break; // we found a match
					
					moreInclude = includeSpans.Next(); // intersected: keep scanning
				}
				return moreInclude;
			}
			
			public virtual bool SkipTo(int target)
			{
				if (moreInclude)
				// skip include
					moreInclude = includeSpans.SkipTo(target);
				
				if (!moreInclude)
					return false;
				
				if (moreExclude && includeSpans.Doc() > excludeSpans.Doc())
					moreExclude = excludeSpans.SkipTo(includeSpans.Doc());
				
				while (moreExclude && includeSpans.Doc() == excludeSpans.Doc() && excludeSpans.End() <= includeSpans.Start())
				{
					moreExclude = excludeSpans.Next(); // increment exclude
				}
				
				if (!moreExclude || includeSpans.Doc() != excludeSpans.Doc() || includeSpans.End() <= excludeSpans.Start())
					return true; // we found a match
				
				return Next(); // scan to next match
			}
			
			public virtual int Doc()
			{
				return includeSpans.Doc();
			}
			public virtual int Start()
			{
				return includeSpans.Start();
			}
			public virtual int End()
			{
				return includeSpans.End();
			}
			
			public override System.String ToString()
			{
				return "spans(" + Enclosing_Instance.ToString() + ")";
			}
		}
		private SpanQuery include;
		private SpanQuery exclude;
		
		/// <summary>Construct a SpanNotQuery matching spans from <code>include</code> which
		/// have no overlap with spans from <code>exclude</code>.
		/// </summary>
		public SpanNotQuery(SpanQuery include, SpanQuery exclude)
		{
			this.include = include;
			this.exclude = exclude;
			
			if (!include.GetField().Equals(exclude.GetField()))
				throw new System.ArgumentException("Clauses must have same field.");
		}
		
		/// <summary>Return the SpanQuery whose matches are filtered. </summary>
		public virtual SpanQuery GetInclude()
		{
			return include;
		}
		
		/// <summary>Return the SpanQuery whose matches must not overlap those returned. </summary>
		public virtual SpanQuery GetExclude()
		{
			return exclude;
		}
		
		public override System.String GetField()
		{
			return include.GetField();
		}
		
        /// <summary>Returns a collection of all terms matched by this query.</summary>
        /// <deprecated> use extractTerms instead
        /// </deprecated>
        /// <seealso cref="#ExtractTerms(Set)">
        /// </seealso>
        public override System.Collections.ICollection GetTerms()
		{
			return include.GetTerms();
		}

        public override void  ExtractTerms(System.Collections.Hashtable terms)
        {
            include.ExtractTerms(terms);
        }
		
		public override System.String ToString(System.String field)
		{
			System.Text.StringBuilder buffer = new System.Text.StringBuilder();
			buffer.Append("spanNot(");
			buffer.Append(include.ToString(field));
			buffer.Append(", ");
			buffer.Append(exclude.ToString(field));
			buffer.Append(")");
			buffer.Append(ToStringUtils.Boost(GetBoost()));
			return buffer.ToString();
		}
		
		
		public override Spans GetSpans(IndexReader reader)
		{
			return new AnonymousClassSpans(reader, this);
		}
		
		public override Query Rewrite(IndexReader reader)
		{
			SpanNotQuery clone = null;
			
			SpanQuery rewrittenInclude = (SpanQuery) include.Rewrite(reader);
			if (rewrittenInclude != include)
			{
				clone = (SpanNotQuery) this.Clone();
				clone.include = rewrittenInclude;
			}
			SpanQuery rewrittenExclude = (SpanQuery) exclude.Rewrite(reader);
			if (rewrittenExclude != exclude)
			{
				if (clone == null)
					clone = (SpanNotQuery) this.Clone();
				clone.exclude = rewrittenExclude;
			}
			
			if (clone != null)
			{
				return clone; // some clauses rewrote
			}
			else
			{
				return this; // no clauses rewrote
			}
		}
		
        /// <summary>Returns true iff <code>o</code> is equal to this. </summary>
        public  override bool Equals(System.Object o)
        {
            if (this == o)
                return true;
            if (!(o is SpanNotQuery))
                return false;
			
            SpanNotQuery other = (SpanNotQuery) o;
            return this.include.Equals(other.include) && this.exclude.Equals(other.exclude) && this.GetBoost() == other.GetBoost();
        }
		
        public override int GetHashCode()
        {
            int h = include.GetHashCode();
            h = (h << 1) | ((int) (((uint) h) >> 31)); // rotate left
            h ^= exclude.GetHashCode();
            h = (h << 1) | ((int) (((uint) h) >> 31)); // rotate left
            h ^= System.Convert.ToInt32(GetBoost());
            return h;
        }
    }
}