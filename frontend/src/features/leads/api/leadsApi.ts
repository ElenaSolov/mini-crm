import { api } from '../../../lib/api';
import type { Lead, LeadInput, LeadPage, LeadsQuery, LeadStatus, TrackerEvent } from '../types';

// Cache tags: { type: 'Lead', id: 'LIST' } is the leads table, { type: 'Lead', id }
// is one lead. A change marks the tags it affects, and RTK Query reloads only
// the answers that carry them.
const leadsList = { type: 'Lead', id: 'LIST' } as const;

export const leadsApi = api.injectEndpoints({
  endpoints: (build) => ({
    getLeads: build.query<LeadPage, LeadsQuery>({
      // fetchBaseQuery leaves undefined params out of the URL.
      query: (filters) => ({ url: 'leads', params: filters }),
      providesTags: [leadsList],
    }),

    getLead: build.query<Lead, number>({
      query: (id) => `leads/${id}`,
      providesTags: (_result, _error, id) => [{ type: 'Lead', id }],
    }),

    getStatuses: build.query<LeadStatus[], void>({
      query: () => 'leads/statuses',
    }),

    getLeadEvents: build.query<TrackerEvent[], number>({
      query: (id) => `leads/${id}/events`,
      providesTags: (_result, _error, id) => [{ type: 'Events', id }],
    }),

    createLead: build.mutation<Lead, LeadInput>({
      query: (input) => ({ url: 'leads', method: 'POST', body: input }),
      invalidatesTags: [leadsList],
    }),

    updateLead: build.mutation<Lead, { id: number; input: LeadInput }>({
      query: ({ id, input }) => ({ url: `leads/${id}`, method: 'PUT', body: input }),
      invalidatesTags: (_result, _error, { id }) => [leadsList, { type: 'Lead', id }],
    }),

    deleteLead: build.mutation<void, number>({
      query: (id) => ({ url: `leads/${id}`, method: 'DELETE' }),
      invalidatesTags: [leadsList],
    }),

    // Converting creates or re-queues the tracker event, so the events reload too.
    changeStatus: build.mutation<Lead, { id: number; status: LeadStatus; simulateServerError: boolean }>({
      query: ({ id, status, simulateServerError }) => ({
        url: `leads/${id}/status`,
        method: 'POST',
        body: { status, simulate_server_error: simulateServerError },
      }),
      invalidatesTags: (_result, _error, { id }) => [leadsList, { type: 'Lead', id }, { type: 'Events', id }],
    }),
  }),
});

export const {
  useGetLeadsQuery,
  useGetLeadQuery,
  useGetStatusesQuery,
  useGetLeadEventsQuery,
  useCreateLeadMutation,
  useUpdateLeadMutation,
  useDeleteLeadMutation,
  useChangeStatusMutation,
} = leadsApi;
