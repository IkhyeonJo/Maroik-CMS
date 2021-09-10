DROP DATABASE IF EXISTS MyLaboratory;

CREATE DATABASE MyLaboratory default charset=utf8mb4 collate utf8mb4_unicode_ci;

USE MyLaboratory;

-- MySQL dump 10.19  Distrib 10.2.38-MariaDB, for debian-linux-gnu (x86_64)
--
-- Host: localhost    Database: MyLaboratory
-- ------------------------------------------------------
-- Server version	10.2.38-MariaDB-1:10.2.38+maria~bionic

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `Account`
--

DROP TABLE IF EXISTS `Account`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Account` (
  `Email` varchar(255) NOT NULL COMMENT '계정 이메일 (ID)',
  `HashedPassword` longtext NOT NULL COMMENT '계정 암호화 된 비밀번호',
  `FullName` varchar(255) NOT NULL COMMENT '계정 성명',
  `AvatarImagePath` varchar(255) NOT NULL DEFAULT '/upload/Management/Profile/default-avatar.jpg' COMMENT '계정 아바타 이미지 경로',
  `Role` varchar(255) NOT NULL DEFAULT 'User' COMMENT '계정 역할 (Admin 또는 User)',
  `Locked` tinyint(1) NOT NULL COMMENT '계정 잠금',
  `LoginAttempt` int(11) NOT NULL COMMENT '로그인 시도 횟수',
  `EmailConfirmed` tinyint(1) NOT NULL COMMENT '이메일 확인 여부',
  `AgreedServiceTerms` tinyint(1) NOT NULL COMMENT '약관 동의 여부',
  `RegistrationToken` longtext DEFAULT NULL COMMENT '회원가입 인증 토큰',
  `ResetPasswordToken` longtext DEFAULT NULL COMMENT '비밀번호 찾기 인증 토큰',
  `Created` datetime(6) NOT NULL DEFAULT '1900-01-01 00:00:00.000000' COMMENT '계정 생성일',
  `Updated` datetime(6) NOT NULL DEFAULT '1900-01-01 00:00:00.000000' COMMENT '계정 업데이트일',
  `Message` longtext DEFAULT NULL COMMENT '계정 상태 메시지',
  `Deleted` tinyint(1) NOT NULL COMMENT '계정 삭제 여부',
  PRIMARY KEY (`Email`),
  CONSTRAINT `Account_Role_check` CHECK (`Role` = 'Admin' or `Role` = 'User')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='MyLaboratory.WebSite 계정';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Account`
--

LOCK TABLES `Account` WRITE;
/*!40000 ALTER TABLE `Account` DISABLE KEYS */;
INSERT INTO `Account` VALUES ('user@gmail.com','R6uvPTQE5d6CXaRkYjnCxONqMIQxR5wxyuDvlmes/Tn+uYK91OtWWTo4wxOXqgzdiT1O8N2xAPxDTBluN+mDz+svw1BBB7rFdu7tVhCRxd9jbXfGBnLqk2nIzx0GQw64MfvcXwv8Kafn5SHxAw2vdZFYZk36xbeibk6GfexEclxmNB/83CAeOOddYCJDeiDEzK6mqbc/FarS/SduSgeEhZEThc2h4aQyZCtsQEsg8ej43tFfhgkXFLEAOn4qcYYure8VtQOa8c/E6r6kVZ1bDNW+WdfHyVFE26bFrQaPA/3ApegryJLfsNTUG8bFQ7MUElv5dX81rxQSztU9Ke2u0g==','UserName','/upload/Management/Profile/default-avatar.jpg','User',0,0,1,1,NULL,NULL,'2021-07-12 22:46:59.000000','2021-09-08 07:38:00.230358','Success to reset password',0),('admin@gmail.com','R6uvPTQE5d6CXaRkYjnCxONqMIQxR5wxyuDvlmes/Tn+uYK91OtWWTo4wxOXqgzdiT1O8N2xAPxDTBluN+mDz+svw1BBB7rFdu7tVhCRxd9jbXfGBnLqk2nIzx0GQw64MfvcXwv8Kafn5SHxAw2vdZFYZk36xbeibk6GfexEclxmNB/83CAeOOddYCJDeiDEzK6mqbc/FarS/SduSgeEhZEThc2h4aQyZCtsQEsg8ej43tFfhgkXFLEAOn4qcYYure8VtQOa8c/E6r6kVZ1bDNW+WdfHyVFE26bFrQaPA/3ApegryJLfsNTUG8bFQ7MUElv5dX81rxQSztU9Ke2u0g==','AdminUser','/upload/Management/Profile/default-avatar.jpg','Admin',0,0,1,1,NULL,NULL,'2021-07-12 22:37:36.000000','2021-07-12 22:37:36.000000','Success',0);
/*!40000 ALTER TABLE `Account` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `Asset`
--

DROP TABLE IF EXISTS `Asset`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Asset` (
  `ProductName` varchar(255) NOT NULL COMMENT '상품명 (은행 계좌명, 증권 계좌명, 현금 등)',
  `AccountEmail` varchar(255) NOT NULL COMMENT '계정 이메일 (ID)',
  `Item` varchar(255) NOT NULL COMMENT '항목 (자유입출금 자산, 신탁 자산, 현금 자산, 저축성 자산, 투자성 자산, 부동산, 동산, 기타 실물 자산, 보험 자산)',
  `Amount` bigint(255) NOT NULL COMMENT '금액',
  `MonetaryUnit` varchar(45) NOT NULL COMMENT '화폐 단위 (KRW, USD, ETC)',
  `Created` datetime(6) NOT NULL COMMENT '생성일',
  `Updated` datetime(6) NOT NULL COMMENT '업데이트일',
  `Note` varchar(45) DEFAULT NULL COMMENT '비고',
  `Deleted` tinyint(1) NOT NULL COMMENT '삭제여부',
  PRIMARY KEY (`ProductName`,`AccountEmail`),
  KEY `Asset_FK` (`AccountEmail`),
  CONSTRAINT `Asset_FK` FOREIGN KEY (`AccountEmail`) REFERENCES `Account` (`Email`) ON DELETE NO ACTION ON UPDATE CASCADE,
  CONSTRAINT `Asset_Item_check` CHECK (`Item` = 'FreeDepositAndWithdrawal' or `Item` = 'TrustAsset' or `Item` = 'CashAsset' or `Item` = 'SavingsAsset' or `Item` = 'InvestmentAsset' or `Item` = 'RealEstate' or `Item` = 'Movables' or `Item` = 'OtherPhysicalAsset' or `Item` = 'InsuranceAsset')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='MyLaboratory.WebSite 자산';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Asset`
--

LOCK TABLES `Asset` WRITE;
/*!40000 ALTER TABLE `Asset` DISABLE KEYS */;
/*!40000 ALTER TABLE `Asset` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `Category`
--

DROP TABLE IF EXISTS `Category`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Category` (
  `Id` int(11) NOT NULL AUTO_INCREMENT COMMENT 'ID',
  `Name` varchar(256) NOT NULL COMMENT '이름',
  `DisplayName` varchar(256) NOT NULL COMMENT '표시이름',
  `IconPath` varchar(256) NOT NULL COMMENT '표시 아이콘 경로 /*FontAwesome 사용*/',
  `Controller` varchar(256) NOT NULL COMMENT '접근 MVC Controller 명',
  `Action` varchar(256) DEFAULT NULL COMMENT '접근 MVC Action 명 /*이 값이 없으면 하위 카테고리 존재*/',
  `Role` varchar(256) NOT NULL DEFAULT 'Admin' COMMENT '접근 권한 설정',
  `Order` int(11) NOT NULL COMMENT '출력 순서',
  PRIMARY KEY (`Id`),
  CONSTRAINT `Category_Role_check` CHECK (`Role` = 'Admin' or `Role` = 'User')
) ENGINE=InnoDB AUTO_INCREMENT=10 DEFAULT CHARSET=utf8 COMMENT='카테고리 /*MyLaboratory.WebSite의 로그인 후 접근 가능한 좌측 SideBar 설정 시 사용*/';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Category`
--

LOCK TABLES `Category` WRITE;
/*!40000 ALTER TABLE `Category` DISABLE KEYS */;
INSERT INTO `Category` VALUES (1,'DashBoard','DashBoard','nav-icon fas fa-tachometer-alt','DashBoard','AdminIndex','Admin',0),(2,'Management','Management','nav-icon fas fa-cog','Management','','Admin',2),(3,'DashBoard','DashBoard','nav-icon fas fa-tachometer-alt','DashBoard','UserIndex','User',0),(4,'Management','Management','nav-icon fas fa-cog','Management','','User',3),(7,'Develop','Develop','nav-icon fas fa-code','Develop','','Admin',1),(8,'AccountBook','AccountBook','nav-icon fas fa-dollar-sign','AccountBook','','User',2),(9,'Notice','Notice','nav-icon fas fa-bell','Notice','','User',1);
/*!40000 ALTER TABLE `Category` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `Expenditure`
--

DROP TABLE IF EXISTS `Expenditure`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Expenditure` (
  `Id` int(11) NOT NULL AUTO_INCREMENT COMMENT 'PK',
  `AccountEmail` varchar(255) NOT NULL COMMENT '계정 이메일 (ID)',
  `MainClass` varchar(255) NOT NULL COMMENT '대분류 (정기저축/비소비지출/소비지출)',
  `SubClass` varchar(255) NOT NULL COMMENT '소분류 (예적금/내자산이체/투자 | 공적연금/부채상환/세금/사회보험/가구간 이전지출/비영리단체 이전 | (식비/외식비)/(주거/용품비)/교육비/의료비/교통비/통신비/(여가/문화)/(의류/신발)/용돈/보장성보험/기타지출/미파악지출)',
  `Contents` varchar(255) NOT NULL COMMENT '내용 (A마트/B카드/C음식점/D도서관)',
  `Amount` bigint(255) NOT NULL COMMENT '금액',
  `PaymentMethod` varchar(255) NOT NULL COMMENT '결제 수단 (자산 상품명/현금)',
  `MyDepositAsset` varchar(255) DEFAULT NULL COMMENT '내 입금 자산 (자산 상품명/현금) (지출 중 [예적금, 내자산이체, 투자, 공적연금, 부채상환]일 때 사용)',
  `Created` datetime(6) NOT NULL COMMENT '생성일',
  `Updated` datetime(6) NOT NULL COMMENT '업데이트일',
  `Note` varchar(45) DEFAULT NULL COMMENT '비고',
  PRIMARY KEY (`Id`),
  KEY `Expenditure_FK` (`PaymentMethod`,`AccountEmail`),
  CONSTRAINT `Expenditure_FK` FOREIGN KEY (`PaymentMethod`, `AccountEmail`) REFERENCES `Asset` (`ProductName`, `AccountEmail`) ON DELETE NO ACTION ON UPDATE CASCADE,
  CONSTRAINT `Expenditure_MainClass_check` CHECK (`MainClass` = 'RegularSavings' or `MainClass` = 'NonConsumerSpending' or `MainClass` = 'ConsumerSpending'),
  CONSTRAINT `Expenditure_SubClass_check` CHECK (`SubClass` = 'Deposit' or `SubClass` = 'MyAssetTransfer' or `SubClass` = 'Investment' or `SubClass` = 'PublicPension' or `SubClass` = 'DebtRepayment' or `SubClass` = 'Tax' or `SubClass` = 'SocialInsurance' or `SubClass` = 'InterHouseholdTranserExpenses' or `SubClass` = 'NonProfitOrganizationTransfer' or `SubClass` = 'MealOrEatOutExpenses' or `SubClass` = 'HousingOrSuppliesCost' or `SubClass` = 'EducationExpenses' or `SubClass` = 'MedicalExpenses' or `SubClass` = 'TransportationCost' or `SubClass` = 'CommunicationCost' or `SubClass` = 'LeisureOrCulture' or `SubClass` = 'ClothingOrShoes' or `SubClass` = 'PinMoney' or `SubClass` = 'ProtectionTypeInsurance' or `SubClass` = 'OtherExpenses' or `SubClass` = 'UnknownExpenditure')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='MyLaboratory.WebSite 지출';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Expenditure`
--

LOCK TABLES `Expenditure` WRITE;
/*!40000 ALTER TABLE `Expenditure` DISABLE KEYS */;
/*!40000 ALTER TABLE `Expenditure` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `FixedExpenditure`
--

DROP TABLE IF EXISTS `FixedExpenditure`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `FixedExpenditure` (
  `Id` int(11) NOT NULL AUTO_INCREMENT COMMENT 'PK',
  `AccountEmail` varchar(255) NOT NULL COMMENT '계정 이메일 (ID)',
  `MainClass` varchar(255) NOT NULL COMMENT '대분류 (정기저축/비소비지출/소비지출)',
  `SubClass` varchar(255) NOT NULL COMMENT '소분류 (예적금/내자산이체/투자 | 공적연금/부채상환/세금/사회보험/가구간 이전지출/비영리단체 이전 | (식비/외식비)/(주거/용품비)/교육비/의료비/교통비/통신비/(여가/문화)/(의류/신발)/용돈/보장성보험/기타지출/미파악지출)',
  `Contents` varchar(255) NOT NULL COMMENT '내용 (A마트/B카드/C음식점/D도서관)',
  `Amount` bigint(255) NOT NULL COMMENT '금액',
  `PaymentMethod` varchar(255) NOT NULL COMMENT '결제 수단 (자산 상품명/현금)',
  `MyDepositAsset` varchar(255) DEFAULT NULL COMMENT '내 입금 자산 (자산 상품명/현금) (지출 중 [예적금, 내자산이체, 투자, 공적연금, 부채상환]일 때 사용)',
  `DepositMonth` tinyint(2) unsigned NOT NULL COMMENT '입금월',
  `DepositDay` tinyint(2) unsigned NOT NULL COMMENT '입금일',
  `MaturityDate` datetime(6) NOT NULL COMMENT '만기일',
  `Created` datetime(6) NOT NULL COMMENT '생성일',
  `Updated` datetime(6) NOT NULL COMMENT '업데이트일',
  `Note` varchar(45) DEFAULT NULL COMMENT '비고',
  PRIMARY KEY (`Id`),
  KEY `FixedExpenditure_FK` (`PaymentMethod`,`AccountEmail`),
  CONSTRAINT `FixedExpenditure_FK` FOREIGN KEY (`PaymentMethod`, `AccountEmail`) REFERENCES `Asset` (`ProductName`, `AccountEmail`) ON DELETE NO ACTION ON UPDATE CASCADE,
  CONSTRAINT `FixedExpenditure_MainClass_check` CHECK (`MainClass` = 'RegularSavings' or `MainClass` = 'NonConsumerSpending' or `MainClass` = 'ConsumerSpending'),
  CONSTRAINT `FixedExpenditure_SubClass_check` CHECK (`SubClass` = 'Deposit' or `SubClass` = 'MyAssetTransfer' or `SubClass` = 'Investment' or `SubClass` = 'PublicPension' or `SubClass` = 'DebtRepayment' or `SubClass` = 'Tax' or `SubClass` = 'SocialInsurance' or `SubClass` = 'InterHouseholdTranserExpenses' or `SubClass` = 'NonProfitOrganizationTransfer' or `SubClass` = 'MealOrEatOutExpenses' or `SubClass` = 'HousingOrSuppliesCost' or `SubClass` = 'EducationExpenses' or `SubClass` = 'MedicalExpenses' or `SubClass` = 'TransportationCost' or `SubClass` = 'CommunicationCost' or `SubClass` = 'LeisureOrCulture' or `SubClass` = 'ClothingOrShoes' or `SubClass` = 'PinMoney' or `SubClass` = 'ProtectionTypeInsurance' or `SubClass` = 'OtherExpenses' or `SubClass` = 'UnknownExpenditure')
) ENGINE=InnoDB AUTO_INCREMENT=49 DEFAULT CHARSET=utf8mb4 COMMENT='MyLaboratory.WebSite 고정지출';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `FixedExpenditure`
--

LOCK TABLES `FixedExpenditure` WRITE;
/*!40000 ALTER TABLE `FixedExpenditure` DISABLE KEYS */;
/*!40000 ALTER TABLE `FixedExpenditure` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `FixedIncome`
--

DROP TABLE IF EXISTS `FixedIncome`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `FixedIncome` (
  `Id` int(11) NOT NULL AUTO_INCREMENT COMMENT 'PK',
  `AccountEmail` varchar(255) NOT NULL COMMENT '계정 이메일 (ID)',
  `MainClass` varchar(255) NOT NULL COMMENT '대분류 (정기수입/비정기수입)',
  `SubClass` varchar(255) NOT NULL COMMENT '소분류 (근로수입/사업수입/연금수입/금융소득/임대수입/기타수입)',
  `Contents` varchar(255) NOT NULL COMMENT '내용 (회사명/사업명)',
  `Amount` bigint(255) NOT NULL COMMENT '금액',
  `DepositMyAssetProductName` varchar(255) NOT NULL COMMENT '입금 자산 (자산 상품명/현금)',
  `DepositMonth` tinyint(2) unsigned NOT NULL COMMENT '입금월',
  `DepositDay` tinyint(2) unsigned NOT NULL COMMENT '입금일',
  `MaturityDate` datetime(6) NOT NULL COMMENT '만기일',
  `Created` datetime(6) NOT NULL COMMENT '생성일',
  `Updated` datetime(6) NOT NULL COMMENT '업데이트일',
  `Note` varchar(45) DEFAULT NULL COMMENT '비고',
  PRIMARY KEY (`Id`),
  KEY `FixedIncome_FK` (`DepositMyAssetProductName`,`AccountEmail`),
  CONSTRAINT `FixedIncome_FK` FOREIGN KEY (`DepositMyAssetProductName`, `AccountEmail`) REFERENCES `Asset` (`ProductName`, `AccountEmail`) ON DELETE NO ACTION ON UPDATE CASCADE,
  CONSTRAINT `FixedIncome_MainClass_check` CHECK (`MainClass` = 'RegularIncome' or `MainClass` = 'IrregularIncome'),
  CONSTRAINT `FixedIncome_SubClass_check` CHECK (`SubClass` = 'LaborIncome' or `SubClass` = 'BusinessIncome' or `SubClass` = 'PensionIncome' or `SubClass` = 'FinancialIncome' or `SubClass` = 'RentalIncome' or `SubClass` = 'OtherIncome')
) ENGINE=InnoDB AUTO_INCREMENT=17 DEFAULT CHARSET=utf8mb4 COMMENT='MyLaboratory.WebSite 고정수입';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `FixedIncome`
--

LOCK TABLES `FixedIncome` WRITE;
/*!40000 ALTER TABLE `FixedIncome` DISABLE KEYS */;
/*!40000 ALTER TABLE `FixedIncome` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `Income`
--

DROP TABLE IF EXISTS `Income`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Income` (
  `Id` int(11) NOT NULL AUTO_INCREMENT COMMENT 'PK',
  `AccountEmail` varchar(255) NOT NULL COMMENT '계정 이메일 (ID)',
  `MainClass` varchar(255) NOT NULL COMMENT '대분류 (정기수입/비정기수입)',
  `SubClass` varchar(255) NOT NULL COMMENT '소분류 (근로수입/사업수입/연금수입/금융소득/임대수입/기타수입)',
  `Contents` varchar(255) NOT NULL COMMENT '내용 (회사명/사업명)',
  `Amount` bigint(255) NOT NULL COMMENT '금액',
  `DepositMyAssetProductName` varchar(255) NOT NULL COMMENT '입금 자산 (자산 상품명/현금)',
  `Created` datetime(6) NOT NULL COMMENT '생성일',
  `Updated` datetime(6) NOT NULL COMMENT '업데이트일',
  `Note` varchar(45) DEFAULT NULL COMMENT '비고',
  PRIMARY KEY (`Id`),
  KEY `Income_FK` (`DepositMyAssetProductName`,`AccountEmail`),
  CONSTRAINT `Income_FK` FOREIGN KEY (`DepositMyAssetProductName`, `AccountEmail`) REFERENCES `Asset` (`ProductName`, `AccountEmail`) ON DELETE NO ACTION ON UPDATE CASCADE,
  CONSTRAINT `Income_MainClass_check` CHECK (`MainClass` = 'RegularIncome' or `MainClass` = 'IrregularIncome'),
  CONSTRAINT `Income_SubClass_check` CHECK (`SubClass` = 'LaborIncome' or `SubClass` = 'BusinessIncome' or `SubClass` = 'PensionIncome' or `SubClass` = 'FinancialIncome' or `SubClass` = 'RentalIncome' or `SubClass` = 'OtherIncome')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='MyLaboratory.WebSite 수입';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Income`
--

LOCK TABLES `Income` WRITE;
/*!40000 ALTER TABLE `Income` DISABLE KEYS */;
/*!40000 ALTER TABLE `Income` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `SubCategory`
--

DROP TABLE IF EXISTS `SubCategory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `SubCategory` (
  `Id` int(11) NOT NULL AUTO_INCREMENT COMMENT 'ID',
  `CategoryId` int(11) NOT NULL COMMENT '부모 카테고리 ID',
  `Name` varchar(256) NOT NULL COMMENT '이름',
  `DisplayName` varchar(256) NOT NULL COMMENT '표시이름',
  `IconPath` varchar(256) NOT NULL COMMENT '표시 아이콘 경로 /*FontAwesome 사용*/',
  `Action` varchar(256) NOT NULL COMMENT '접근 MVC Action 명 /*이 값이 없으면 하위 카테고리 존재*/',
  `Role` varchar(256) NOT NULL DEFAULT 'Admin' COMMENT '접근 권한 설정',
  `Order` int(11) NOT NULL COMMENT '출력 순서',
  PRIMARY KEY (`Id`),
  KEY `SubCategory_FK` (`CategoryId`),
  CONSTRAINT `SubCategory_FK` FOREIGN KEY (`CategoryId`) REFERENCES `Category` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `SubCategory_Role_check` CHECK (`Role` = 'Admin' or `Role` = 'User')
) ENGINE=InnoDB AUTO_INCREMENT=18 DEFAULT CHARSET=utf8 COMMENT='서브 카테고리 /*MyLaboratory.WebSite의 로그인 후 접근 가능한 좌측 SideBar 설정 시 사용*/';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `SubCategory`
--

LOCK TABLES `SubCategory` WRITE;
/*!40000 ALTER TABLE `SubCategory` DISABLE KEYS */;
INSERT INTO `SubCategory` VALUES (1,2,'Profile','Profile','far fa-circle nav-icon','Profile','Admin',0),(2,2,'Account','Account','far fa-circle nav-icon','Account','Admin',1),(3,2,'Menu','Menu','far fa-circle nav-icon','Menu','Admin',2),(4,4,'Profile','Profile','far fa-circle nav-icon','Profile','User',0),(11,7,'API','API','far fa-circle nav-icon','API','Admin',0),(12,8,'Asset','Asset','far fa-circle nav-icon','Asset','User',0),(14,8,'Income','Income','far fa-circle nav-icon','Income','User',1),(15,8,'Expenditure','Expenditure','far fa-circle nav-icon','Expenditure','User',2),(16,9,'FixedIncome','FixedIncome','far fa-circle nav-icon','FixedIncome','User',0),(17,9,'FixedExpenditure','FixedExpenditure','far fa-circle nav-icon','FixedExpenditure','User',1);
/*!40000 ALTER TABLE `SubCategory` ENABLE KEYS */;
UNLOCK TABLES;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2021-09-08 20:16:32