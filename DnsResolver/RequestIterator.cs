using System.Net;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;

namespace DnsResolver;

public class ImmediatlyAnswerException : Exception
{
   public IPAddress Answer { set; get; }

   public ImmediatlyAnswerException(IPAddress address)
   {
      this.Answer = address;
   }
}

public class RequestIterator
{
   private readonly CachedRequester requester = new CachedRequester();
   private readonly Resolver resolver;
   
   public void ResetRequestCounter()
   {
      requester.ResetRequestCounter();
   }
   
   public RequestIterator(Resolver resolver)
   {
      this.resolver = resolver;
   }

   // Bool - A request needed
   private IEnumerable<(Domain, IPAddress, bool)> GetNextNsRecords(
      IEnumerable<IPAddress> currentAddresses, String nextName)
   {
      foreach (var currentAddress in currentAddresses)
      {
         Response nsResponse = new Response();
         try
         {
            var nsQuestion = new Question(Domain.FromString(nextName), RecordType.NS);
            nsResponse = requester.GetResponseFromQuestion(currentAddress, nsQuestion);
         }
         catch (CachedRequester.RequestCounterMoreThanMax)
         {
            throw;
         }
         catch (Exception e)
         {
            Log.Logger.Information("Ns request failed: " + e.ToString());
            continue;
         }

         Log.Logger.Debug("Checking CNAME records before all");
         foreach (var cnameRecord in nsResponse.AuthorityRecords.Concat(nsResponse.AnswerRecords))
         {
            if (cnameRecord is CanonicalNameResourceRecord cnameRecordCasted)
            {
               foreach (var finalAddress in resolver.ResolveEnumerable(cnameRecordCasted.CanonicalDomainName.ToString()))
               {
                  Log.Logger.Debug($"Final address from cname: {finalAddress}");
                  throw new ImmediatlyAnswerException(finalAddress);
               }
            }
         }

         foreach (var nsRecord in nsResponse.AuthorityRecords.Concat(nsResponse.AnswerRecords))
         {
            if (nsRecord is NameServerResourceRecord nsRecordCasted)
            {
               foreach (var additionalRecord in nsResponse.AdditionalRecords)
               {
                  if (nsRecordCasted.NSDomainName.Equals(additionalRecord.Name))
                  {
                     if (additionalRecord is IPAddressResourceRecord additionalRecordCasted)
                     {
                        Log.Logger.Debug(
                           $"Ns ({additionalRecordCasted.Type}) record in additional: {additionalRecordCasted.IPAddress} ({additionalRecordCasted.Name})");
                        yield return (nsRecordCasted.NSDomainName, additionalRecordCasted.IPAddress, false);
                     }
                  }
               } 
            }
         }
         
         foreach (var nsRecord in nsResponse.AuthorityRecords.Concat(nsResponse.AnswerRecords))
         {
            if (nsRecord is NameServerResourceRecord nsRecordCasted)
            {
               Log.Logger.Debug("Ns record: " + nsRecordCasted.NSDomainName);
               yield return (nsRecordCasted.NSDomainName, currentAddress, true);
            }
         }
         
         Log.Logger.Debug("Checking SOA records");

         foreach (var soaRecord in nsResponse.AuthorityRecords.Concat(nsResponse.AnswerRecords))
         {
            if (soaRecord is StartOfAuthorityResourceRecord soaRecordCasted)
            {
               if (soaRecordCasted.MasterDomainName.ToString() == String.Empty)
               {
                  Log.Logger.Information($"Soa record with root");
                  continue;
               }
               Log.Logger.Debug($"Soa record {soaRecordCasted.MasterDomainName}");
               if (soaRecordCasted.MasterDomainName.ToString() == nextName)
               {
                  bool isCnameAndSoa = false;
                  foreach (var cnameRecord in nsResponse.AuthorityRecords.Concat(nsResponse.AnswerRecords))
                  {
                     if (cnameRecord is CanonicalNameResourceRecord cnameRecordCasted)
                     {
                        Log.Logger.Debug($"Soa and cname {cnameRecordCasted.CanonicalDomainName}");
                        isCnameAndSoa = true;
                        foreach (var finalAddress in GetFinalAddresses(new List<IPAddress>{currentAddress}, cnameRecordCasted.CanonicalDomainName.ToString()))
                        {
                           Log.Logger.Debug($"Final address from cname and soa: {finalAddress}");
                           throw new ImmediatlyAnswerException(finalAddress);
                        }
                        Log.Logger.Error("Cname error (Not found a record)");
                        yield break;
                     }
                  }

                  if (!isCnameAndSoa)
                  {
                     Log.Logger.Debug($"Soa record equal to required ns {soaRecordCasted.MasterDomainName} -> address {currentAddress}");
                     yield return (soaRecordCasted.MasterDomainName, currentAddress, true); 
                     //Log.Logger.Debug($"Try with ns {soaRecordCasted.Name} and address {currentAddress}");
                     //yield return (soaRecordCasted.Name, currentAddress, true);
                  }
               }
               else
               {
                  foreach (var cnameRecord in nsResponse.AuthorityRecords.Concat(nsResponse.AnswerRecords))
                  {
                     if (cnameRecord is CanonicalNameResourceRecord cnameRecordCasted)
                     {
                        Log.Logger.Debug($"Soa and cname {cnameRecordCasted.CanonicalDomainName}");
                        foreach (var finalAddress in GetFinalAddresses(new List<IPAddress>{currentAddress}, cnameRecordCasted.CanonicalDomainName.ToString()))
                        {
                           Log.Logger.Debug($"Final address from cname and soa: {finalAddress}");
                           throw new ImmediatlyAnswerException(finalAddress);
                        }
                     }
                  }
                  
                  {
                     Log.Logger.Debug($"Try with current address with soa {soaRecordCasted.MasterDomainName} -> address {currentAddress}");
                     yield return (soaRecordCasted.MasterDomainName, currentAddress, true); 
                  }
                  
                  foreach (var soaAddress in resolver.ResolveEnumerable(soaRecordCasted.MasterDomainName.ToString()))
                  {
                     Log.Logger.Debug($"Ns record address from soa: {soaAddress}");
                     yield return (soaRecordCasted.MasterDomainName, soaAddress, false); 
                  }
               }
            }
         }
         
         Log.Logger.Debug("Checking CNAME records");
         
         foreach (var cnameRecord in nsResponse.AuthorityRecords.Concat(nsResponse.AnswerRecords))
         {
            if (cnameRecord is CanonicalNameResourceRecord cnameRecordCasted)
            {
               foreach (var finalAddress in resolver.ResolveEnumerable(cnameRecordCasted.CanonicalDomainName.ToString()))
               {
                  Log.Logger.Debug($"Final address from cname: {finalAddress}");
                  throw new ImmediatlyAnswerException(finalAddress);
               }
            }
         }
         
         Log.Logger.Information("Ns response does not contains needed information");
      }
   }

   public IEnumerable<IPAddress> GetNextNsIpAddress(IEnumerable<IPAddress> currentAddresses, String nextName)
   {
      foreach (var (nsDomainName, currentAddress, aRequestNeeded) in GetNextNsRecords(currentAddresses, nextName))
      {
         if (aRequestNeeded)
         {
            foreach (var recordType in new List<RecordType> { RecordType.A, RecordType.AAAA })
            {
               Response aResponse = new Response();
               try
               {
                  var aQuestion = new Question(nsDomainName, recordType);
                  aResponse = requester.GetResponseFromQuestion(currentAddress, aQuestion);
               }
               catch (CachedRequester.RequestCounterMoreThanMax)
               {
                  throw;
               }
               catch (Exception e)
               {
                  Log.Logger.Information($"Ns ({recordType}) request failed: " + e.ToString());
                  continue;
               }

               foreach (var aRecord in aResponse.AnswerRecords)
               {
                  if (aRecord is IPAddressResourceRecord aRecordCasted)
                  {
                     if (aRecordCasted.Type == recordType && aRecordCasted.Name.Equals(nsDomainName))
                     {
                        Log.Logger.Debug($"Ns ({recordType}) record in answer: {aRecordCasted.IPAddress} ({aRecordCasted.Name})");
                        yield return aRecordCasted.IPAddress;
                     }
                  }
               }
               
               foreach (var aRecord in aResponse.AdditionalRecords)
               {
                  if (aRecord is IPAddressResourceRecord aRecordCasted)
                  {
                     if (aRecordCasted.Type == recordType && aRecordCasted.Name.Equals(nsDomainName))
                     {
                        Log.Logger.Debug($"Ns ({recordType}) record: {aRecordCasted.IPAddress} ({aRecordCasted.Name})");
                        yield return aRecordCasted.IPAddress;
                     }
                  }
               }

               Log.Logger.Information($"Ns ({recordType}) response does not contains needed information");

               foreach (var nsAddress in resolver.ResolveEnumerable(nsDomainName.ToString()))
               {
                  Log.Logger.Debug($"Ns record after resolve: {nsAddress}");
                  yield return nsAddress;
               }
            }
         }
         else
         {
            Log.Logger.Debug($"Ns record from GetNextNsRecords: {currentAddress} ({nsDomainName})");
            yield return currentAddress;
         }
      }
   }

   public IEnumerable<IPAddress> GetFinalAddresses(IEnumerable<IPAddress> currentAddresses, String nextName, RecordType requestedRecordType = RecordType.A)
   {
      foreach (var currentAddress in currentAddresses)
      {
         foreach (var recordType in new List<RecordType>{RecordType.A/*, RecordType.AAAA*/})
         {
            if (requestedRecordType != RecordType.ANY && recordType != requestedRecordType)
            {
               continue;
            }
            
            Response aResponse = new Response();
            try
            {
               var aQuestion = new Question(Domain.FromString(nextName), recordType);
               aResponse = requester.GetResponseFromQuestion(currentAddress, aQuestion);
            }
            catch (CachedRequester.RequestCounterMoreThanMax)
            {
               throw;
            }
            catch (Exception e)
            {
               Log.Logger.Information($"Final address ({recordType}) after resolve ns request failed: " + e.ToString());
               continue;
            }

            foreach (var aRecord in aResponse.AnswerRecords)
            {
               if (aRecord is IPAddressResourceRecord aRecordCasted/* && aRecord.Name.ToString() == nextName*/)
               {
                  if (aRecordCasted.Type == recordType)
                  {
                     Log.Logger.Debug(
                        $"Final address ({recordType}) record: {aRecordCasted.IPAddress} ({aRecordCasted.Name})");
                     yield return aRecordCasted.IPAddress;
                  }
               }
            }
            
            Log.Logger.Information($"Final address ({recordType}) response does not contains needed information");
         }
      }
   }
}