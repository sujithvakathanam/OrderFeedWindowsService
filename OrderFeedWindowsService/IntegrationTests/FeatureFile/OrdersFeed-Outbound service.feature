Feature: OrderFeedFunctionatity
	In order to notify the shipped Orders to external application
	As an Outbound service
	I want to be able to send a report of all the shipped orders
	And I want to raise an event in the MSM queue

	Background: 
	Given the ExportFolder_OrdersFeed directory contains no files

@OrderFeed
Scenario: Updation of OrderFeed table 
	Given the following order has been shipped
	| OrderId | CustomerId | EmployeeId | OrderFeed |
	| 1001    | CENTC      | 4          | 0         |
	| 1002    | ERNSH      | 1          | 0         |
	When the OrderFeed Service is run
	| ServiceName |
	|  OrderFeedService |
	Then the OrdersTable should be Updated with below OrderFeedStatus information
	| OrderId | OrderFeed |
	| 1001    | 1         |
	| 1002    | 1         |

@OrderFeed
Scenario: Generation of XML file
	Given the following order has been shipped
	| OrderId | CustomerId | EmployeeId | OrderFeed |
	| 4001    | CENTC      | 4          | 0         |
	| 4002    | ERNSH      | 1          | 0         |
	When the OrderFeed Service is run
	| ServiceName |
	|  OrderFeedService |
	Then 1 file should be generated with name OrderFeed<datetimestamp>
	And the file should contain the following details
	"""
	<?xml version="1.0" encoding="utf-8" standalone="yes"?>
	<!--Order Feed information-->
	<Orders>
	  <Order>
	    <OrderId>4001</OrderId>
		<CustomerId>CENTC</CustomerId>
		<ShipName>Queen Cozinhaa</ShipName>
		<ShipCountry>USA</ShipCountry>
	  </Order>
	  <Order>
	    <OrderId>4002</OrderId>
		<CustomerId>ERNSH</CustomerId>
		<ShipName>Queen Cozinhaa</ShipName>
		<ShipCountry>USA</ShipCountry>
	  </Order>
	</Orders>
	"""

@OrderFeed
Scenario: No Duplicate XML files are generated
	Given the following order has been shipped 
	| OrderId | CustomerId | EmployeeId | OrderFeed |
	| 5001    | CENTC      | 4          | 1         |
	| 5002    | ERNSH      | 1          | 1         |
	When the OrderFeed Service is run
	| ServiceName |
	| OrderFeedService   |
	Then 0 file should be generated with name OrderFeed<datetimestamp>

@OrderFeed
@PurgeMessage
Scenario: Generation of OrderFeedMessages in MSM queue
	Given there are no messages in orderfeedqueue
	Given the following order has been shipped
	| OrderId | CustomerId | EmployeeId | OrderFeed |
	| 9001    | CENTC      | 4          | 0         |
	When the OrderFeed Service is run
	| ServiceName |
	|  OrderFeedService |
	Then the below message should be raised in MSM queue
	| QueueName      | MessageBody                                         |
	| orderfeedqueue | Event raised for Customer CENTC with OrderId as 9001 |

@OrderFeed
Scenario: Non generation of OrderFeedMessages 
	Given there are no messages in orderfeedqueue
	Given the following order has been shipped
	| OrderId | CustomerId | EmployeeId | OrderFeed |
	| 7001    | CENTC      | 4          | 1         |
	When the OrderFeed Service is run
    | ServiceName |
    |OrderFeedService |
	Then no message should be raised in orderfeedqueue queue



